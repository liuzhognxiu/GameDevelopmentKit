param(
    [switch]$RequireStaticComplete,
    [switch]$RequireComplete,
    [switch]$RefreshSourceFingerprints
)

$ErrorActionPreference = 'Stop'
if ($RequireComplete) { $RequireStaticComplete = $true }

$kbRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $kbRoot
$catalogPath = Join-Path $kbRoot 'catalog.json'
$fingerprintPath = Join-Path $kbRoot 'source-fingerprints.json'
$acceptancePath = Join-Path $kbRoot 'runtime-acceptance.json'
$allowedStatuses = @('planned', 'seed', 'verified')
$requiredRuntimeIds = @('client-start', 'server-start', 'luban-export', 'proto-generation', 'target-player-build')
$runtimeRoots = @('Unity', 'DotNet', 'Share', 'Tools', 'Design', 'Config', 'Book', 'Kit.sln')
$fingerprintAlgorithm = 'sha256-path-git-clean-oid-v2'
$requiredHeadings = @(
    Get-Content -LiteralPath (Join-Path $kbRoot '_template.md') -Encoding UTF8 |
        Where-Object { $_.StartsWith('## ') }
)
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$documents = @{}
$tick = [string][char]0x60
$catalogIdRegex = [regex]::Escape($tick) + '(?<id>[A-Z]+-\d{2})' + [regex]::Escape($tick)
$marker = [string][char]0x5F85 + [char]0x786E + [char]0x8BA4
$waitChar = [string][char]0x7B49
$markerPattern = '(?<!' + [regex]::Escape($waitChar) + ')' + [regex]::Escape($marker)

function Normalize-Path([string]$Path) {
    return ($Path -replace '\\', '/').TrimStart('/')
}

function Get-Doc([string]$Path) {
    if (-not $documents.ContainsKey($Path)) {
        $documents[$Path] = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    }
    return $documents[$Path]
}

function Compare-ExactSet($Expected, $Actual) {
    return @(Compare-Object -ReferenceObject @($Expected | Sort-Object) -DifferenceObject @($Actual | Sort-Object))
}

function Invoke-GitRequired([string[]]$Arguments, [string]$FailureMessage) {
    $output = @(& git -C $repoRoot @Arguments 2>&1 | ForEach-Object { [string]$_ })
    if ($LASTEXITCODE -ne 0) {
        $detail = ($output -join [Environment]::NewLine).Trim()
        if ([string]::IsNullOrWhiteSpace($detail)) { throw "$FailureMessage Git exited with code $LASTEXITCODE." }
        throw "$FailureMessage Git exited with code $LASTEXITCODE. $detail"
    }
    return $output
}

function Set-GitCleanObjectCache($RelativePaths, $HashCache) {
    $missing = @(
        $RelativePaths |
            Sort-Object -Unique |
            Where-Object { -not $HashCache.ContainsKey($_) }
    )
    if ($missing.Count -eq 0) { return }

    # Windows PowerShell 5 adds a UTF-8 BOM to native-command stdin. Feed Git a
    # BOM-free path list through redirection so the first repository path is exact.
    $pathList = [IO.Path]::GetTempFileName()
    try {
        [IO.File]::WriteAllLines($pathList, $missing, [Text.UTF8Encoding]::new($false))
        $commandLine = 'git.exe -C "' + $repoRoot + '" hash-object --stdin-paths < "' + $pathList + '"'
        $output = @(& cmd.exe /d /s /c $commandLine 2>&1 | ForEach-Object { [string]$_ })
    }
    finally {
        [IO.File]::Delete($pathList)
    }
    if ($LASTEXITCODE -ne 0) {
        $detail = ($output -join [Environment]::NewLine).Trim()
        if ([string]::IsNullOrWhiteSpace($detail)) { throw "Unable to fingerprint tracked sources through git clean filters. Git exited with code $LASTEXITCODE." }
        throw "Unable to fingerprint tracked sources through git clean filters. Git exited with code $LASTEXITCODE. $detail"
    }
    if ($output.Count -ne $missing.Count) {
        throw "git hash-object returned $($output.Count) object id(s) for $($missing.Count) source path(s)."
    }

    for ($i = 0; $i -lt $missing.Count; $i++) {
        $objectId = $output[$i].Trim()
        if ([string]::IsNullOrWhiteSpace($objectId) -or $objectId -notmatch '^[0-9a-fA-F]{40,64}$') {
            throw "git hash-object returned an invalid object id for $($missing[$i])"
        }
        $HashCache[$missing[$i]] = $objectId.ToLowerInvariant()
    }
}

function Get-Fingerprint($Module, $Tracked, $TrackedSet, $HashCache) {
    $files = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($sourceValue in @($Module.sources)) {
        $source = Normalize-Path $sourceValue
        $absolute = Join-Path $repoRoot ($source -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $absolute -PathType Leaf) {
            if ($TrackedSet.Contains($source)) { [void]$files.Add($source) }
        }
        else {
            $prefix = $source.TrimEnd('/') + '/'
            foreach ($trackedFile in $Tracked) {
                if ($trackedFile.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                    [void]$files.Add($trackedFile)
                }
            }
        }
    }

    $regularFiles = [System.Collections.Generic.List[string]]::new()
    foreach ($relative in @($files | Sort-Object)) {
        $absolute = Join-Path $repoRoot ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
        if ((Test-Path -LiteralPath $absolute -PathType Leaf) -and
            -not (Test-Path -LiteralPath $absolute -PathType Container)) {
            [void]$regularFiles.Add($relative)
        }
    }
    Set-GitCleanObjectCache -RelativePaths $regularFiles -HashCache $HashCache

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($relative in @($files | Sort-Object)) {
        $absolute = Join-Path $repoRoot ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (Test-Path -LiteralPath $absolute -PathType Container) {
            $gitObject = [string](& git -C $repoRoot rev-parse (':' + $relative))
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitObject)) {
                $errors.Add("[$($Module.id)] Unable to fingerprint tracked gitlink: $relative")
            }
            else {
                $lines.Add($relative + [char]0 + 'gitlink:' + $gitObject.Trim())
            }
            continue
        }
        if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) {
            $errors.Add("[$($Module.id)] Tracked source file is missing: $relative")
            continue
        }
        if (-not $HashCache.ContainsKey($relative)) {
            throw "[$($Module.id)] Missing git clean object id for tracked source: $relative"
        }
        $lines.Add($relative + [char]0 + 'git-clean-oid:' + $HashCache[$relative])
    }

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join [char]10))
        $hash = ([BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
    return [pscustomobject][ordered]@{ id = [string]$Module.id; fileCount = $lines.Count; fingerprint = $hash }
}

if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    Write-Host "ERROR: catalog not found: $catalogPath"
    exit 1
}
try {
    $catalog = Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    Write-Host "ERROR: invalid catalog JSON: $($_.Exception.Message)"
    exit 1
}

if ($catalog.schemaVersion -ne 1) { $errors.Add("Unsupported catalog schemaVersion: $($catalog.schemaVersion)") }
$modules = @($catalog.modules)
if ($modules.Count -eq 0) { $errors.Add('Catalog contains no modules.') }
foreach ($duplicate in @($modules | Group-Object id | Where-Object Count -gt 1)) {
    $errors.Add("Duplicate module id: $($duplicate.Name)")
}

foreach ($module in $modules) {
    if ([string]::IsNullOrWhiteSpace($module.id)) { $errors.Add('A module is missing id.'); continue }
    if ($module.status -notin $allowedStatuses) { $errors.Add("[$($module.id)] Invalid status: $($module.status)") }
    if ([string]::IsNullOrWhiteSpace($module.document)) { $errors.Add("[$($module.id)] Missing document path.") }
    if (@($module.sources).Count -eq 0) { $errors.Add("[$($module.id)] No source paths declared.") }
    foreach ($source in @($module.sources)) {
        $absolute = Join-Path $repoRoot ((Normalize-Path $source) -replace '/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $absolute)) { $errors.Add("[$($module.id)] Missing source path: $source") }
    }
}

$groups = @(
    $modules |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.document) } |
        Group-Object { Normalize-Path $_.document }
)
foreach ($group in $groups) {
    $relative = $group.Name
    $path = Join-Path $repoRoot ($relative -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if (@($group.Group | Where-Object status -ne 'planned').Count -gt 0) { $errors.Add("Missing document: $relative") }
        else { $warnings.Add("Missing planned document: $relative") }
        continue
    }

    $text = Get-Doc $path
    $lines = @([regex]::Split($text, '\r?\n'))
    $knownIds = @($modules | ForEach-Object id)
    foreach ($referencedId in @([regex]::Matches($text, $catalogIdRegex) | ForEach-Object { $_.Groups['id'].Value } | Sort-Object -Unique)) {
        if ($referencedId -notin $knownIds) {
            $errors.Add("Unknown Catalog ID reference '$referencedId': $relative")
        }
    }
    if (@($group.Group | Where-Object status -eq 'verified').Count -gt 0) {
        foreach ($heading in $requiredHeadings) {
            if ($lines -notcontains $heading) { $errors.Add("Missing heading '$heading': $relative") }
        }
    }

    $metadataIndex = -1
    for ($i = 0; $i -lt [Math]::Min(12, $lines.Count); $i++) {
        if ($lines[$i].StartsWith('> Catalog ID:')) { $metadataIndex = $i; break }
    }
    if ($metadataIndex -lt 0 -or $metadataIndex + 3 -ge $lines.Count) {
        $errors.Add("Missing fixed four-line metadata: $relative")
        continue
    }

    $actualIds = @([regex]::Matches($lines[$metadataIndex], $catalogIdRegex) | ForEach-Object { $_.Groups['id'].Value } | Sort-Object -Unique)
    $expectedIds = @($group.Group | ForEach-Object id | Sort-Object -Unique)
    if ((Compare-ExactSet $expectedIds $actualIds).Count -gt 0) {
        $errors.Add("Catalog ID metadata does not match mapping: $relative")
    }

    $statusRegex = '^> [^' + [regex]::Escape($tick) + ']+' + [regex]::Escape($tick) +
        '(?<value>planned|seed|verified)' + [regex]::Escape($tick) + '\s{2}$'
    $statusMatch = [regex]::Match($lines[$metadataIndex + 1], $statusRegex)
    $expectedStatuses = @($group.Group | ForEach-Object status | Sort-Object -Unique)
    if (-not $statusMatch.Success -or $expectedStatuses.Count -ne 1 -or
        $statusMatch.Groups['value'].Value -ne $expectedStatuses[0]) {
        $errors.Add("Invalid or mismatched status metadata: $relative")
    }

    $dateRegex = '^> [^' + [regex]::Escape($tick) + ']+' + [regex]::Escape($tick) +
        '(?<value>\d{4}-\d{2}-\d{2})' + [regex]::Escape($tick) + '\s{2}$'
    $dateMatch = [regex]::Match($lines[$metadataIndex + 2], $dateRegex)
    $parsedDate = [DateTime]::MinValue
    if (-not $dateMatch.Success -or -not [DateTime]::TryParseExact(
        $dateMatch.Groups['value'].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None, [ref]$parsedDate
    )) {
        $errors.Add("Invalid verification date metadata: $relative")
    }
    if (-not [regex]::IsMatch($lines[$metadataIndex + 3], '^> \S.+$')) {
        $errors.Add("Invalid applicable mode metadata: $relative")
    }
}

$catalogDocs = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($group in $groups) { [void]$catalogDocs.Add($group.Name) }
$numberedDocs = @(
    Get-ChildItem -LiteralPath $kbRoot -File | Where-Object Name -Match '^\d{2}-.+\.md$' | Sort-Object Name
)
foreach ($doc in $numberedDocs) {
    $relative = Normalize-Path ('KnowledgeBase/' + $doc.Name)
    if (-not $catalogDocs.Contains($relative)) { $errors.Add("Unmapped numbered document: $relative") }
}
for ($i = 0; $i -lt $numberedDocs.Count; $i++) {
    if ([int]$numberedDocs[$i].Name.Substring(0, 2) -ne $i + 1) {
        $errors.Add("Numbered documents are not contiguous at: $($numberedDocs[$i].Name)")
        break
    }
}

$docPaths = @(
    $groups | ForEach-Object { Join-Path $repoRoot ($_.Name -replace '/', [IO.Path]::DirectorySeparatorChar) } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Sort-Object -Unique
)
$linkDocPaths = @(
    $docPaths
    Join-Path $kbRoot 'README.md'
    Join-Path $kbRoot 'LOOP.md'
    Join-Path $kbRoot '_template.md'
) | Sort-Object -Unique
foreach ($docPath in $linkDocPaths) {
    $text = Get-Doc $docPath
    $directory = Split-Path -Parent $docPath
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^\s)#]+\.md)(?:#[^)]*)?\)')) {
        $target = $match.Groups['target'].Value
        if ($target -match '^[a-z][a-z0-9+.-]*:') { continue }
        if (-not (Test-Path -LiteralPath (Join-Path $directory ($target -replace '/', [IO.Path]::DirectorySeparatorChar)) -PathType Leaf)) {
            $errors.Add(("Broken Markdown link in {0}: {1}" -f $docPath, $target))
        }
    }
}

$readmeText = Get-Doc (Join-Path $kbRoot 'README.md')
$readmeCountMatch = [regex]::Match($readmeText, 'catalog\.json[^\r\n]*?(?<count>\d+)[^\r\n]*')
if (-not $readmeCountMatch.Success -or [int]$readmeCountMatch.Groups['count'].Value -ne $modules.Count) {
    $errors.Add('README module count does not match catalog.')
}

$tracked = @(Invoke-GitRequired `
    -Arguments @('ls-files', '--cached', '--others', '--exclude-standard') `
    -FailureMessage 'Unable to enumerate tracked files with git.' |
    ForEach-Object { Normalize-Path $_ })
$trackedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $tracked) { [void]$trackedSet.Add($item) }
$sources = @($modules | ForEach-Object sources | ForEach-Object { Normalize-Path $_ } | Sort-Object -Unique)
$candidates = @(
    $tracked | Where-Object {
        $_ -match '^[^/]+\.(sln|ps1)$' -or
        $_ -match '^(DotNet|Share)/.+\.csproj$' -or
        $_ -match '^Unity/Assets/Scripts/.+\.(asmdef|asmref)$' -or
        $_ -match '^(Tools/Shell|Design)/.+\.(bat|ps1|sh)$'
    }
)
foreach ($candidate in $candidates) {
    $covered = $false
    foreach ($source in $sources) {
        $absolute = Join-Path $repoRoot ($source -replace '/', [IO.Path]::DirectorySeparatorChar)
        if ($candidate.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
            ((Test-Path -LiteralPath $absolute -PathType Container) -and
             $candidate.StartsWith($source.TrimEnd('/') + '/', [StringComparison]::OrdinalIgnoreCase))) {
            $covered = $true
            break
        }
    }
    if (-not $covered) { $errors.Add("Uncovered project, assembly, or script entry: $candidate") }
}

$hashCache = @{}
$currentFingerprints = @(foreach ($module in $modules) { Get-Fingerprint $module $tracked $trackedSet $hashCache })
if ($RefreshSourceFingerprints) {
    if ($errors.Count -eq 0) {
        $data = [pscustomobject][ordered]@{
            schemaVersion = 1
            algorithm = $fingerprintAlgorithm
            modules = $currentFingerprints
        }
        $json = $data | ConvertTo-Json -Depth 5
        [IO.File]::WriteAllText($fingerprintPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
        Write-Host "Refreshed source fingerprints: $fingerprintPath"
    }
    else { $errors.Add('Fingerprints were not refreshed because validation has errors.') }
}
elseif (-not (Test-Path -LiteralPath $fingerprintPath -PathType Leaf)) {
    $errors.Add('Missing source-fingerprints.json; review sources and run -RefreshSourceFingerprints.')
}
else {
    try {
        $savedData = Get-Content -LiteralPath $fingerprintPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($savedData.schemaVersion -ne 1 -or $savedData.algorithm -ne $fingerprintAlgorithm) {
            $errors.Add("Unsupported fingerprint schema or algorithm: schemaVersion=$($savedData.schemaVersion), algorithm='$($savedData.algorithm)'. Expected schemaVersion=1, algorithm='$fingerprintAlgorithm'.")
        }
        else {
            $saved = @{}
            foreach ($entry in @($savedData.modules)) {
                if ($saved.ContainsKey([string]$entry.id)) { $errors.Add("Duplicate fingerprint id: $($entry.id)") }
                else { $saved[[string]$entry.id] = $entry }
            }
            foreach ($current in $currentFingerprints) {
                if (-not $saved.ContainsKey($current.id)) { $errors.Add("[$($current.id)] Missing source fingerprint."); continue }
                if ([int]$saved[$current.id].fileCount -ne $current.fileCount -or
                    [string]$saved[$current.id].fingerprint -ne $current.fingerprint) {
                    $errors.Add("[$($current.id)] Sources changed; re-review and refresh fingerprints.")
                }
            }
            foreach ($savedId in $saved.Keys) {
                if ($savedId -notin @($modules.id)) { $errors.Add("Unknown fingerprint module id: $savedId") }
            }
        }
    }
    catch { $errors.Add("Invalid source-fingerprints.json: $($_.Exception.Message)") }
}

$acceptance = $null
if (-not (Test-Path -LiteralPath $acceptancePath -PathType Leaf)) { $errors.Add('Missing runtime-acceptance.json.') }
else {
    try {
        $acceptance = Get-Content -LiteralPath $acceptancePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $topProperties = @($acceptance.PSObject.Properties | ForEach-Object Name)
        if ((Compare-ExactSet @('schemaVersion', 'sourceRevision', 'checks') $topProperties).Count -gt 0) {
            $errors.Add('runtime-acceptance.json has invalid top-level properties.')
        }
        if ($acceptance.schemaVersion -ne 1) { $errors.Add("Unsupported acceptance schemaVersion: $($acceptance.schemaVersion)") }
        $checks = @($acceptance.checks)
        $checkIds = @($checks | ForEach-Object id)
        if ((Compare-ExactSet $requiredRuntimeIds $checkIds).Count -gt 0 -or
            @($checks | Group-Object id | Where-Object Count -gt 1).Count -gt 0) {
            $errors.Add('Runtime acceptance must contain exactly five required unique ids.')
        }
        foreach ($check in $checks) {
            $properties = @($check.PSObject.Properties | ForEach-Object Name)
            if ((Compare-ExactSet @('id', 'status', 'performedAt', 'environment', 'procedure', 'evidence', 'notes') $properties).Count -gt 0) {
                $errors.Add("Runtime check '$($check.id)' has invalid properties.")
            }
            if ($check.status -notin @('not_run', 'blocked', 'passed', 'failed')) {
                $errors.Add("Runtime check '$($check.id)' has invalid status.")
            }
            if ([string]::IsNullOrWhiteSpace($check.procedure) -or $check.procedure -notmatch '^(?<path>[^#]+\.md)#.+$') {
                $errors.Add("Runtime check '$($check.id)' has invalid procedure.")
            }
            else {
                $procedurePath = Join-Path $repoRoot ((Normalize-Path $Matches['path']) -replace '/', [IO.Path]::DirectorySeparatorChar)
                if (-not (Test-Path -LiteralPath $procedurePath -PathType Leaf)) { $errors.Add("Missing procedure file for '$($check.id)'.") }
            }
            if ($check.status -in @('passed', 'failed')) {
                $time = [DateTimeOffset]::MinValue
                if ([string]::IsNullOrWhiteSpace($check.performedAt) -or
                    -not [DateTimeOffset]::TryParse([string]$check.performedAt, [ref]$time) -or
                    [string]::IsNullOrWhiteSpace($check.environment) -or @($check.evidence).Count -eq 0) {
                    $errors.Add("Runtime check '$($check.id)' requires time, environment, and evidence.")
                }
                foreach ($evidence in @($check.evidence)) {
                    $evidencePath = [string]$evidence
                    if ([string]::IsNullOrWhiteSpace($evidencePath) -or $evidencePath -match '^[a-z][a-z0-9+.-]*:') {
                        $errors.Add("Runtime check '$($check.id)' evidence must be a repository path.")
                        continue
                    }
                    $evidenceFile = $evidencePath -replace '#.*$', ''
                    $absoluteEvidence = Join-Path $repoRoot ((Normalize-Path $evidenceFile) -replace '/', [IO.Path]::DirectorySeparatorChar)
                    if (-not (Test-Path -LiteralPath $absoluteEvidence -PathType Leaf)) {
                        $errors.Add("Runtime check '$($check.id)' evidence file does not exist: $evidencePath")
                    }
                }
            }
            elseif ($check.status -eq 'not_run' -and
                ($null -ne $check.performedAt -or $null -ne $check.environment -or @($check.evidence).Count -ne 0)) {
                $errors.Add("Runtime check '$($check.id)' has invalid not_run fields.")
            }
            elseif ($check.status -eq 'blocked' -and [string]::IsNullOrWhiteSpace($check.notes)) {
                $errors.Add("Runtime check '$($check.id)' requires blocked notes.")
            }
        }
    }
    catch { $errors.Add("Invalid runtime-acceptance.json: $($_.Exception.Message)") }
}

if ($RequireStaticComplete) {
    foreach ($module in @($modules | Where-Object status -ne 'verified')) {
        $errors.Add("[$($module.id)] Static completion requires verified status.")
    }
    foreach ($docPath in $docPaths) {
        if ([regex]::IsMatch((Get-Doc $docPath), $markerPattern)) {
            $errors.Add("Static completion forbids unresolved markers: $docPath")
        }
    }
}

if ($RequireComplete -and $null -ne $acceptance) {
    foreach ($check in @($acceptance.checks | Where-Object status -ne 'passed')) {
        $errors.Add("Runtime completion requires '$($check.id)' to be passed; current status is $($check.status).")
    }
    $revision = [string]$acceptance.sourceRevision
    if ($revision -notmatch '^[0-9a-fA-F]{40}$') { $errors.Add('Runtime completion requires a full Git sourceRevision.') }
    else {
        & git -C $repoRoot cat-file -e ($revision + '^{commit}') 2>$null
        if ($LASTEXITCODE -ne 0) { $errors.Add("Acceptance revision is not a local commit: $revision") }
        else {
            $changed = @(& git -C $repoRoot diff --name-only $revision -- @runtimeRoots)
            $dirty = @(& git -C $repoRoot status --porcelain -- @runtimeRoots)
            if ($changed.Count -gt 0) { $errors.Add('Runtime acceptance is stale after source changes.') }
            if ($dirty.Count -gt 0) { $errors.Add('Runtime completion requires clean runtime sources.') }
        }
    }
}

Write-Host "Knowledge base catalog: $($modules.Count) modules"
Write-Host "Knowledge base documents: $($numberedDocs.Count) numbered documents"
foreach ($group in ($modules | Group-Object status | Sort-Object Name)) {
    Write-Host ("  {0,-8} {1,3}" -f $group.Name, $group.Count)
}
if ($null -ne $acceptance) {
    Write-Host 'Runtime acceptance:'
    foreach ($group in (@($acceptance.checks) | Group-Object status | Sort-Object Name)) {
        Write-Host ("  {0,-8} {1,3}" -f $group.Name, $group.Count)
    }
}
foreach ($warning in $warnings) { Write-Host "WARNING: $warning" }
foreach ($problem in $errors) { Write-Host "ERROR: $problem" }
if ($errors.Count -gt 0) {
    Write-Host "Validation failed with $($errors.Count) error(s) and $($warnings.Count) warning(s)."
    exit 1
}
Write-Host "Validation passed with $($warnings.Count) warning(s)."
exit 0
