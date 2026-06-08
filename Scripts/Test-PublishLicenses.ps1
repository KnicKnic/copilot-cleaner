[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'

$publishPath = Resolve-Path $PublishDirectory
$licensePath = Join-Path $publishPath 'LICENSE'
$noticePath = Join-Path $publishPath 'THIRD_PARTY_NOTICES.md'

if (-not (Test-Path $licensePath)) {
    throw "Publish output is missing LICENSE: $licensePath"
}

if (-not (Test-Path $noticePath)) {
    throw "Publish output is missing THIRD_PARTY_NOTICES.md: $noticePath"
}

$copilotCliDirectories = @(Get-ChildItem $publishPath -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'copilot-cli' })
foreach ($directory in $copilotCliDirectories) {
    $licenseFiles = @(Get-ChildItem $directory.FullName -Filter 'LICENSE.md' -File -Recurse -ErrorAction SilentlyContinue)
    if ($licenseFiles.Count -eq 0) {
        throw "Publish output contains $($directory.FullName) but no bundled GitHub Copilot CLI LICENSE.md file."
    }
}

Write-Host "Publish license file check passed for $publishPath."