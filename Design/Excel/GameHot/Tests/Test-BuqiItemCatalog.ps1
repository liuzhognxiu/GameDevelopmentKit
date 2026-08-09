[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ReferenceCsv
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
}

function Assert-CatalogCondition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-TaggedCount {
    param(
        [object[]]$Items,
        [string]$Tag
    )

    return @($Items | Where-Object { @($_.Tags) -contains $Tag }).Count
}

$jsonPath = Join-Path $RepositoryRoot 'Unity\Assets\Res\Editor\Hot\Luban\dtbuqiitem.json'
$rowPath = Join-Path $RepositoryRoot 'Unity\Assets\Scripts\Game\Hot\Code\Generate\Luban\DRBuqiItem.cs'
$parsedItems = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$items = @($parsedItems | ForEach-Object { $_ })

Assert-CatalogCondition ($items.Count -eq 300) "Expected exactly 300 items, found $($items.Count)."

$expectedIds = @(1..300 | ForEach-Object { 'W8-{0:D3}' -f $_ })
$actualIds = @($items.DefinitionId | Sort-Object)
Assert-CatalogCondition (($actualIds -join '|') -eq ($expectedIds -join '|')) 'Definition IDs must be the contiguous range W8-001..W8-300.'
Assert-CatalogCondition ((@($items.DisplayName | Sort-Object -Unique).Count) -eq 300) 'Display names must be unique.'
$itemIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($id in $actualIds) {
    [void]$itemIds.Add([string]$id)
}

$source = Get-Content -LiteralPath $rowPath -Raw -Encoding UTF8
Assert-CatalogCondition ($source.Contains('public readonly string DesignNote;')) 'Generated row is missing DesignNote.'
Assert-CatalogCondition ($source.Contains('public readonly string EffectDescription;')) 'Generated row is missing EffectDescription.'
Assert-CatalogCondition ($source.Contains('public readonly string LocalizationKey;')) 'Generated row is missing LocalizationKey.'
Assert-CatalogCondition ($source.Contains('public readonly string Role;')) 'Generated row is missing Role.'
Assert-CatalogCondition ($source.Contains('public readonly int UnlockDay;')) 'Generated row is missing UnlockDay.'
Assert-CatalogCondition ($source.Contains('public readonly System.Collections.Generic.List<BuqiRunEffectConfig> RunEffects;')) 'Generated row is missing RunEffects.'
Assert-CatalogCondition ($source.Contains('public readonly System.Collections.Generic.List<string> LinkIds;')) 'Generated row is missing LinkIds.'

$linkTags = @('attack', 'shield', 'haste', 'delay', 'charge', 'heal', 'regen', 'poison', 'burn', 'freeze', 'overload', 'adjacent', 'counter', 'sustain')
$reasonCodes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($item in $items) {
    $where = [string]$item.DefinitionId
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.DesignNote)) "$where has no Chinese design note."
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.EffectDescription)) "$where has no formal effect description."
    Assert-CatalogCondition ([string]$item.DesignNote -match '[\u4e00-\u9fff]') "$where design note must contain Chinese text."
    Assert-CatalogCondition ([string]$item.EffectDescription -match '[\u4e00-\u9fff]') "$where effect description must contain Chinese text."
    Assert-CatalogCondition ([string]$item.LocalizationKey -eq "Buqi.Content.Item.$($where.Replace('-', '_')).Name") "$where has an invalid name localization key."
    Assert-CatalogCondition ([string]$item.UpgradeLocalizationKey -eq "Buqi.Content.Item.$($where.Replace('-', '_')).Upgrade") "$where has an invalid upgrade localization key."
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.UpgradeSummary)) "$where has no upgrade summary."
    Assert-CatalogCondition ([string]$item.UpgradeSummary -match '[\u4e00-\u9fff]') "$where upgrade summary must contain Chinese text."
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.PositionHint)) "$where has no position hint."
    Assert-CatalogCondition (@($item.Effects).Count -ge 1) "$where has no runnable effects."
    Assert-CatalogCondition (@($item.Effects).Count -le 3) "$where exceeds the current three-effect content budget."

    $roles = @($item.Tags | Where-Object { $_ -like 'role-*' })
    $days = @($item.Tags | Where-Object { $_ -in @('day1-3', 'day4-6', 'day7-9') })
    $links = @($item.Tags | Where-Object { $_ -in $linkTags } | Sort-Object -Unique)
    Assert-CatalogCondition ($roles.Count -eq 1) "$where must have exactly one role tag."
    Assert-CatalogCondition ($days.Count -eq 1) "$where must have exactly one day-band tag."
    Assert-CatalogCondition ($links.Count -ge 2) "$where must expose at least two mechanical link tags."
    Assert-CatalogCondition ([string]$item.Role -eq ([string]$roles[0]).Substring(5)) "$where role does not match its role tag."
    $expectedUnlockDay = if ($days[0] -eq 'day1-3') { 1 } elseif ($days[0] -eq 'day4-6') { 4 } else { 7 }
    Assert-CatalogCondition ([int]$item.UnlockDay -eq $expectedUnlockDay) "$where unlock day does not match its day-band tag."

    $expectedBaseCost = if ([int]$item.Size -eq 3) { 6 } elseif ([int]$item.Size -eq 2) { 4 } else { 2 }
    $expectedFixedCost = if ([int]$item.Size -eq 3) { 9 } elseif ([int]$item.Size -eq 2) { 6 } else { 3 }
    Assert-CatalogCondition ([int]$item.BasePrice -eq $expectedBaseCost) "$where has an invalid base price."
    Assert-CatalogCondition ([int]$item.ImprovedUpgradeCost -eq $expectedBaseCost) "$where has an invalid improved upgrade cost."
    Assert-CatalogCondition ([int]$item.FixedUpgradeCost -eq $expectedFixedCost) "$where has an invalid fixed upgrade cost."
    Assert-CatalogCondition ([int]$item.RefinementCost -eq $expectedBaseCost) "$where has an invalid refinement cost."

    $linkedIds = @($item.LinkIds)
    Assert-CatalogCondition ($linkedIds.Count -ge 2) "$where must link to at least two items."
    Assert-CatalogCondition (@($linkedIds | Sort-Object -Unique).Count -eq $linkedIds.Count) "$where contains duplicate item links."
    foreach ($linkedId in $linkedIds) {
        Assert-CatalogCondition ([string]$linkedId -ne $where) "$where cannot link to itself."
        Assert-CatalogCondition ($itemIds.Contains([string]$linkedId)) "$where links to unknown item $linkedId."
    }

    $runEffects = @($item.RunEffects)
    if ([string]$item.Role -eq 'economy') {
        Assert-CatalogCondition ($runEffects.Count -eq 1) "$where economy item must define exactly one run effect."
    }
    else {
        Assert-CatalogCondition ($runEffects.Count -eq 0) "$where non-economy item must not define a run effect."
    }
    foreach ($runEffect in $runEffects) {
        Assert-CatalogCondition ([int]$runEffect.Trigger -in 1..4) "$where uses an unsupported run trigger."
        Assert-CatalogCondition ([int]$runEffect.Effect -in 0..1) "$where uses an unsupported run effect."
        Assert-CatalogCondition ([int]$runEffect.Amount -gt 0) "$where run effect amount must be positive."
        Assert-CatalogCondition ([int]$runEffect.MaxPerDay -eq 1) "$where run effect must be capped once per day."
        Assert-CatalogCondition ([string]$runEffect.ReasonCode -eq "$where-economy") "$where has an invalid run-effect reason code."
    }

    foreach ($effect in @($item.Effects)) {
        Assert-CatalogCondition ([int]$effect.Trigger -in 0..5) "$where uses an unsupported trigger."
        Assert-CatalogCondition ([int]$effect.Effect -in 0..10) "$where uses an unsupported effect."
        Assert-CatalogCondition ([int]$effect.Target -in 0..8) "$where uses an unsupported target."
        Assert-CatalogCondition ([int]$effect.ConditionKind -in 0..2) "$where uses an unsupported condition."
        $reasonCode = [string]$effect.ReasonCode
        Assert-CatalogCondition ($reasonCode.StartsWith($where + '-', [System.StringComparison]::Ordinal)) "$where has a non-prefixed reason code."
        Assert-CatalogCondition ($reasonCodes.Add($reasonCode)) "Duplicate reason code: $reasonCode"
    }
}

$expectedSizes = @{ 1 = 132; 2 = 108; 3 = 60 }
foreach ($size in $expectedSizes.Keys) {
    $actual = @($items | Where-Object { [int]$_.Size -eq [int]$size }).Count
    Assert-CatalogCondition ($actual -eq $expectedSizes[$size]) "Size $size expected $($expectedSizes[$size]), found $actual."
}

$expectedBuilds = @{ 1 = 38; 2 = 38; 3 = 38; 4 = 38; 5 = 37; 6 = 37; 7 = 37; 8 = 37 }
foreach ($build in $expectedBuilds.Keys) {
    $actual = @($items | Where-Object { [int]$_.ArchetypeId -eq [int]$build }).Count
    Assert-CatalogCondition ($actual -eq $expectedBuilds[$build]) "Build $build expected $($expectedBuilds[$build]), found $actual."
}

$expectedDays = @{ 'day1-3' = 120; 'day4-6' = 108; 'day7-9' = 72 }
foreach ($day in $expectedDays.Keys) {
    $actual = Get-TaggedCount $items $day
    Assert-CatalogCondition ($actual -eq $expectedDays[$day]) "$day expected $($expectedDays[$day]), found $actual."
}

$expectedRoles = @{
    'role-starter' = 32
    'role-core' = 40
    'role-amplifier' = 40
    'role-finisher' = 32
    'role-bridge' = 56
    'role-pivot' = 24
    'role-counter' = 36
    'role-economy' = 28
    'role-utility' = 12
}
foreach ($role in $expectedRoles.Keys) {
    $actual = Get-TaggedCount $items $role
    Assert-CatalogCondition ($actual -eq $expectedRoles[$role]) "$role expected $($expectedRoles[$role]), found $actual."
}

if (-not [string]::IsNullOrWhiteSpace($ReferenceCsv)) {
    $referenceNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($row in (Import-Csv -LiteralPath $ReferenceCsv)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$row.name)) {
            [void]$referenceNames.Add(([string]$row.name).Trim())
        }
    }
    $copiedNames = @($items | Where-Object { $referenceNames.Contains(([string]$_.DisplayName).Trim()) } | Select-Object -ExpandProperty DisplayName)
    Assert-CatalogCondition ($copiedNames.Count -eq 0) ("Catalog reuses reference names: " + ($copiedNames -join ', '))
}

Write-Output 'PASS: Buqi item catalog contains exactly 300 original, documented, runnable items.'
