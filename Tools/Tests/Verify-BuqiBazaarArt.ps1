[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$assetRoot = Join-Path $repoRoot 'Unity/Assets/Res/UI/UISprite/Buqi/Bazaar'
$failures = [System.Collections.Generic.List[string]]::new()

$assets = @(
    [pscustomobject]@{ Name = 'bazaar-backdrop.png'; Guid = 'd2f73143bb9147a99f741dc69188ac0e'; Sliced = $false },
    [pscustomobject]@{ Name = 'shop-shelf-panel.png'; Guid = 'e3a5f05878d34f5290e40ca750d061df'; Sliced = $true },
    [pscustomobject]@{ Name = 'player-board-panel.png'; Guid = '49f61940f60d439fbe1e0b95cae76406'; Sliced = $true },
    [pscustomobject]@{ Name = 'item-frame.png'; Guid = '92b102a191114c59b3c8b63d3d2330af'; Sliced = $true }
)

function Add-Failure([string]$message) {
    $failures.Add($message)
}

function Require-Text([string]$path, [string]$pattern, [string]$message) {
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Failure "Missing file: $path"
        return
    }

    $text = Get-Content -Raw -Encoding UTF8 -LiteralPath $path
    if ($text -notmatch $pattern) {
        Add-Failure $message
    }
}

foreach ($asset in $assets) {
    $pngPath = Join-Path $assetRoot $asset.Name
    if (-not (Test-Path -LiteralPath $pngPath)) {
        Add-Failure "Missing PNG: $pngPath"
        continue
    }

    $bytes = [System.IO.File]::ReadAllBytes($pngPath)
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    $hasPngSignature = $bytes.Length -ge 24
    for ($index = 0; $hasPngSignature -and $index -lt $signature.Length; $index++) {
        $hasPngSignature = $bytes[$index] -eq $signature[$index]
    }
    if (-not $hasPngSignature) {
        Add-Failure "Invalid PNG signature: $pngPath"
    }
    else {
        $width = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
        $height = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
        if ($width -lt 512 -or $height -lt 512) {
            Add-Failure "PNG is smaller than 512 px on one axis: $pngPath ($width x $height)"
        }
    }

    $metaPath = "$pngPath.meta"
    Require-Text $metaPath "guid:\s+$($asset.Guid)" "Unexpected Unity GUID for $($asset.Name)."
    Require-Text $metaPath 'spriteMode:\s+1' "Sprite mode is not single for $($asset.Name)."
    Require-Text $metaPath 'textureType:\s+8' "Texture type is not Sprite for $($asset.Name)."
    if ($asset.Sliced) {
        Require-Text $metaPath 'spriteBorder:\s+\{x:\s*[1-9]\d*,\s*y:\s*[1-9]\d*,\s*z:\s*[1-9]\d*,\s*w:\s*[1-9]\d*\}' "Missing 9-slice border for $($asset.Name)."
    }
    else {
        Require-Text $metaPath 'spriteBorder:\s+\{x:\s*0,\s*y:\s*0,\s*z:\s*0,\s*w:\s*0\}' "Backdrop must not use a 9-slice border."
    }
}

$fullBuilder = Join-Path $repoRoot 'Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiFullUIBuilder.cs'
$widgetBuilder = Join-Path $repoRoot 'Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBuildWidgetBuilder.cs'
$popupBuilder = Join-Path $repoRoot 'Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiPopupUIBuilder.cs'

Require-Text $fullBuilder 'Assets/Res/UI/UISprite/Buqi/Bazaar/bazaar-backdrop\.png' 'Full UI builder does not load the bazaar backdrop.'
Require-Text $fullBuilder 'Assets/Res/UI/UISprite/Buqi/Bazaar/shop-shelf-panel\.png' 'Full UI builder does not load the shelf panel.'
Require-Text $fullBuilder 'Assets/Res/UI/UISprite/Buqi/Bazaar/player-board-panel\.png' 'Full UI builder does not load the player board panel.'
Require-Text $widgetBuilder 'Assets/Res/UI/UISprite/Buqi/Bazaar/item-frame\.png' 'Widget builder does not load the item frame.'
Require-Text $popupBuilder 'Assets/Res/UI/UISprite/Buqi/Bazaar/item-frame\.png' 'Popup builder does not load the item frame.'

$shopPrefab = Join-Path $repoRoot 'Unity/Assets/Res/UI/UIPrefab/Buqi/Stages/ShopWidget.prefab'
$offerPrefab = Join-Path $repoRoot 'Unity/Assets/Res/UI/UIPrefab/Buqi/OfferCardWidget.prefab'
$detailPrefab = Join-Path $repoRoot 'Unity/Assets/Res/UI/UIForm/Hot/Buqi/BuqiItemDetailForm.prefab'

Require-Text $shopPrefab 'm_Name:\s+ShopWidget(?s:.*?)guid:\s+d2f73143bb9147a99f741dc69188ac0e' 'Shop root does not reference the bazaar backdrop.'
Require-Text $shopPrefab 'm_Name:\s+SellDropZone(?s:.*?)guid:\s+e3a5f05878d34f5290e40ca750d061df' 'Shop sell zone does not reference the shelf panel.'
Require-Text $shopPrefab 'm_Name:\s+PlayerBoard(?s:.*?)guid:\s+49f61940f60d439fbe1e0b95cae76406' 'Player board does not reference the player board panel.'
Require-Text $offerPrefab 'm_Name:\s+OfferCardWidget(?s:.*?)guid:\s+92b102a191114c59b3c8b63d3d2330af' 'Offer card root does not reference the item frame.'
Require-Text $detailPrefab 'm_Name:\s+ItemCard(?s:.*?)guid:\s+92b102a191114c59b3c8b63d3d2330af' 'Item detail card does not reference the item frame.'

if ($failures.Count -gt 0) {
    Write-Host "Buqi bazaar art contract failed ($($failures.Count)):" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Buqi bazaar art contract passed: 4 sprites, 3 builders, and 3 prefabs verified." -ForegroundColor Green
