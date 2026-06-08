[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..' 'CopilotCleaner.csproj'),
    [string]$MainLicensePath = (Join-Path $PSScriptRoot '..' 'LICENSE'),
    [string]$NoticesPath = (Join-Path $PSScriptRoot '..' 'THIRD_PARTY_NOTICES.md'),
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$project = Resolve-Path $ProjectPath
$repoRoot = Split-Path $project -Parent
$mainLicense = Resolve-Path $MainLicensePath
$notices = Resolve-Path $NoticesPath
$failures = @()

if (-not $NoRestore) {
    dotnet restore $project | Out-Host
}

$assetsPath = Join-Path $repoRoot 'obj' 'project.assets.json'
if (-not (Test-Path $assetsPath)) {
    throw "Missing restored assets file: $assetsPath. Run dotnet restore first."
}

$projectText = Get-Content $project -Raw
if ($projectText -notmatch '<Content\s+Include="LICENSE"') {
    $failures += 'CopilotCleaner.csproj must include LICENSE in build and publish output.'
}

if ($projectText -notmatch '<EmbeddedResource\s+Include="LICENSE"') {
    $failures += 'CopilotCleaner.csproj must embed LICENSE so the main license is available in the app UI.'
}

if ($projectText -notmatch 'THIRD_PARTY_NOTICES\.md') {
    $failures += 'CopilotCleaner.csproj must include THIRD_PARTY_NOTICES.md in build and publish output.'
}

if ($projectText -notmatch '<EmbeddedResource\s+Include="THIRD_PARTY_NOTICES\.md"') {
    $failures += 'CopilotCleaner.csproj must embed THIRD_PARTY_NOTICES.md so notices are available in the app UI.'
}

$mainLicenseText = Get-Content $mainLicense -Raw
foreach ($requiredText in @('MIT License', 'Copyright (c) 2026 Nick Maliwacki', 'Permission is hereby granted')) {
    if ($mainLicenseText -notmatch [regex]::Escape($requiredText)) {
        $failures += "LICENSE is missing required main-license text: $requiredText"
    }
}

$noticeText = Get-Content $notices -Raw
foreach ($requiredText in @('Bundled GitHub Copilot CLI Runtime', 'GitHub Copilot CLI License', 'The ANGLE Project Authors')) {
    if ($noticeText -notmatch [regex]::Escape($requiredText)) {
        $failures += "THIRD_PARTY_NOTICES.md is missing required notice text: $requiredText"
    }
}

$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$allowedExpressions = @('MIT')
$allowedFileLicenses = @{
    'Avalonia.Angle.Windows.Natives' = 'LICENSE'
}

$noticeAssetCategories = @('compile', 'runtime', 'runtimeTargets', 'native', 'contentFiles', 'resource', 'frameworkAssemblies')
$packageKeys = [ordered]@{}
foreach ($target in $assets.targets.PSObject.Properties) {
    foreach ($entry in $target.Value.PSObject.Properties) {
        if ($entry.Value.type -ne 'package') {
            continue
        }

        $assetNames = @($entry.Value.PSObject.Properties.Name)
        $hasNoticeRelevantAssets = @($assetNames | Where-Object { $noticeAssetCategories -contains $_ }).Count -gt 0
        if ($hasNoticeRelevantAssets) {
            $packageKeys[$entry.Name] = $true
        }
    }
}

$packages = $packageKeys.Keys |
    ForEach-Object {
        $name, $version = $_ -split '/', 2
        [pscustomobject]@{ Name = $name; Version = $version }
    } |
    Sort-Object Name, Version

foreach ($package in $packages) {
    $packageRoot = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder (Join-Path $package.Name.ToLowerInvariant() $package.Version.ToLowerInvariant())
        if (Test-Path $candidate) {
            $packageRoot = $candidate
            break
        }
    }

    if (-not $packageRoot) {
        $failures += "Package cache entry not found for $($package.Name) $($package.Version)."
        continue
    }

    $nuspecPath = Join-Path $packageRoot ($package.Name.ToLowerInvariant() + '.nuspec')
    if (-not (Test-Path $nuspecPath)) {
        $failures += "NuGet metadata file not found for $($package.Name) $($package.Version): $nuspecPath"
        continue
    }

    [xml]$nuspec = Get-Content $nuspecPath -Raw
    $licenseNode = $nuspec.package.metadata.license
    $licenseType = if ($licenseNode) { [string]$licenseNode.type } else { '' }
    $licenseValue = if ($licenseNode) { [string]$licenseNode.'#text' } else { '' }

    if ($licenseType -eq 'expression') {
        if ($allowedExpressions -notcontains $licenseValue) {
            $failures += "Package $($package.Name) $($package.Version) uses unapproved license expression '$licenseValue'. Update the policy and notices before merging."
        }
    }
    elseif ($licenseType -eq 'file') {
        if (-not $allowedFileLicenses.ContainsKey($package.Name)) {
            $failures += "Package $($package.Name) $($package.Version) uses license file '$licenseValue' but is not in the approved file-license list."
        }
        elseif ($allowedFileLicenses[$package.Name] -ne $licenseValue) {
            $failures += "Package $($package.Name) $($package.Version) license file changed from '$($allowedFileLicenses[$package.Name])' to '$licenseValue'. Review the license and update notices."
        }

        $licenseFile = Join-Path $packageRoot $licenseValue
        if (-not (Test-Path $licenseFile)) {
            $failures += "Package $($package.Name) $($package.Version) declares missing license file: $licenseValue"
        }
    }
    else {
        $failures += "Package $($package.Name) $($package.Version) does not declare a supported NuGet license expression or file."
    }

    $expectedNoticeRow = "| $($package.Name) | $($package.Version) |"
    if ($noticeText -notmatch [regex]::Escape($expectedNoticeRow)) {
        $failures += "THIRD_PARTY_NOTICES.md is missing or has an outdated row for $($package.Name) $($package.Version)."
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "License check passed for $($packages.Count) notice-relevant NuGet packages."