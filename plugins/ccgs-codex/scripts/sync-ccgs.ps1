[CmdletBinding()]
param(
    [string]$SourceRoot
)

$ErrorActionPreference = 'Stop'
$pluginRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $pluginRoot '..\..')).Path

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $repoRoot '.claude'
}
$SourceRoot = (Resolve-Path $SourceRoot).Path

$references = Join-Path $pluginRoot 'skills\ccgs\references'
$workflowTarget = Join-Path $references 'workflows'
$roleTarget = Join-Path $references 'roles'
$studioTarget = Join-Path $references 'studio'
$standardsTarget = Join-Path $references 'standards'

New-Item -ItemType Directory -Force -Path $workflowTarget, $roleTarget, $studioTarget, $standardsTarget | Out-Null

function Get-FrontmatterValue {
    param(
        [string]$Content,
        [string]$Key
    )

    $frontmatter = [regex]::Match($Content, '(?ms)^---\s*\r?\n(?<body>.*?)\r?\n---').Groups['body'].Value
    $match = [regex]::Match($frontmatter, "(?m)^$([regex]::Escape($Key)):\s*(?<value>.+?)\s*$")
    if (-not $match.Success) {
        return ''
    }
    return $match.Groups['value'].Value.Trim().Trim('"').Trim("'")
}

function Convert-CodexReferencePaths {
    param([string]$Content)

    $result = $Content.Replace('.claude/skills/*/SKILL.md', 'references/workflows/*.md')
    $result = $result.Replace('.claude/skills/[name]/SKILL.md', 'references/workflows/[name].md')
    $result = $result.Replace('.claude/docs/', 'references/studio/')
    $result = $result.Replace('.claude/agents/', 'references/roles/')
    $result = $result.Replace('.claude/skills/', 'references/workflows/')
    $result = $result.Replace('.claude/rules/', 'references/standards/')
    return $result
}

$workflowRows = foreach ($source in Get-ChildItem (Join-Path $SourceRoot 'skills') -Directory | Sort-Object Name) {
    $skillFile = Join-Path $source.FullName 'SKILL.md'
    if (-not (Test-Path $skillFile)) {
        continue
    }

    $content = Get-Content -Raw -Encoding UTF8 $skillFile
    $name = Get-FrontmatterValue -Content $content -Key 'name'
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = $source.Name
    }
    $description = Get-FrontmatterValue -Content $content -Key 'description'
    $destination = Join-Path $workflowTarget "$name.md"
    Set-Content -Encoding UTF8 -Path $destination -Value (Convert-CodexReferencePaths $content)
    [pscustomobject]@{ Name = $name; Description = $description }
}

$roleRows = foreach ($source in Get-ChildItem (Join-Path $SourceRoot 'agents') -File -Filter '*.md' | Sort-Object Name) {
    $content = Get-Content -Raw -Encoding UTF8 $source.FullName
    $name = Get-FrontmatterValue -Content $content -Key 'name'
    if ([string]::IsNullOrWhiteSpace($name)) {
        $name = $source.BaseName
    }
    $description = Get-FrontmatterValue -Content $content -Key 'description'
    $destination = Join-Path $roleTarget $source.Name
    Set-Content -Encoding UTF8 -Path $destination -Value (Convert-CodexReferencePaths $content)
    [pscustomobject]@{ Name = $name; Description = $description }
}

Copy-Item -Path (Join-Path $SourceRoot 'docs\*') -Destination $studioTarget -Recurse -Force
Copy-Item -Path (Join-Path $SourceRoot 'rules\*') -Destination $standardsTarget -Recurse -Force
foreach ($document in Get-ChildItem $studioTarget, $standardsTarget -Recurse -File) {
    $content = Get-Content -Raw -Encoding UTF8 $document.FullName
    Set-Content -Encoding UTF8 -Path $document.FullName -Value (Convert-CodexReferencePaths $content)
}
Copy-Item -LiteralPath (Join-Path $SourceRoot 'third-party\Claude-Code-Game-Studios.LICENSE') -Destination (Join-Path $pluginRoot 'LICENSE.upstream') -Force
Copy-Item -LiteralPath (Join-Path $SourceRoot 'UPSTREAM.md') -Destination (Join-Path $references 'UPSTREAM.md') -Force

$workflowIndex = @(
    '# Workflow Index'
    ''
    'Load one workflow at a time. Interpret every source through `compatibility.md`.'
    ''
    '| Workflow | Source description |'
    '|---|---|'
)
$workflowIndex += $workflowRows | ForEach-Object {
    $description = $_.Description -replace '\|', '\|'
    "| [$($_.Name)](workflows/$($_.Name).md) | $description |"
}
Set-Content -Encoding UTF8 -Path (Join-Path $references 'workflow-index.md') -Value $workflowIndex

$roleIndex = @(
    '# Specialist Role Index'
    ''
    'Load only roles needed for the selected workflow. Role prompts are review lenses, not executable Claude agents.'
    ''
    '| Role | Source description |'
    '|---|---|'
)
$roleIndex += $roleRows | ForEach-Object {
    $description = $_.Description -replace '\|', '\|'
    "| [$($_.Name)](roles/$($_.Name).md) | $description |"
}
Set-Content -Encoding UTF8 -Path (Join-Path $references 'role-index.md') -Value $roleIndex

Write-Host "Synced $($workflowRows.Count) workflows and $($roleRows.Count) roles from $SourceRoot"
