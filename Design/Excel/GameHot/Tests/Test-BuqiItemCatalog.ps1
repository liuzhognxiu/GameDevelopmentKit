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

$source = Get-Content -LiteralPath $rowPath -Raw -Encoding UTF8
Assert-CatalogCondition ($source.Contains('public readonly string DesignNote;')) 'Generated row is missing DesignNote.'
Assert-CatalogCondition ($source.Contains('public readonly string EffectDescription;')) 'Generated row is missing EffectDescription.'

$linkTags = @('attack', 'shield', 'haste', 'delay', 'charge', 'heal', 'regen', 'poison', 'burn', 'freeze', 'overload', 'adjacent', 'counter', 'sustain')
$reasonCodes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($item in $items) {
    $where = [string]$item.DefinitionId
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.DesignNote)) "$where has no Chinese design note."
    Assert-CatalogCondition (-not [string]::IsNullOrWhiteSpace([string]$item.EffectDescription)) "$where has no formal effect description."
    Assert-CatalogCondition ([string]$item.DesignNote -match '[\u4e00-\u9fff]') "$where design note must contain Chinese text."
    Assert-CatalogCondition ([string]$item.EffectDescription -match '[\u4e00-\u9fff]') "$where effect description must contain Chinese text."
    Assert-CatalogCondition (@($item.Effects).Count -ge 1) "$where has no runnable effects."
    Assert-CatalogCondition (@($item.Effects).Count -le 3) "$where exceeds the current three-effect content budget."

    $roles = @($item.Tags | Where-Object { $_ -like 'role-*' })
    $days = @($item.Tags | Where-Object { $_ -in @('day1-3', 'day4-6', 'day7-9') })
    $links = @($item.Tags | Where-Object { $_ -in $linkTags } | Sort-Object -Unique)
    Assert-CatalogCondition ($roles.Count -eq 1) "$where must have exactly one role tag."
    Assert-CatalogCondition ($days.Count -eq 1) "$where must have exactly one day-band tag."
    Assert-CatalogCondition ($links.Count -ge 2) "$where must expose at least two mechanical link tags."

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
