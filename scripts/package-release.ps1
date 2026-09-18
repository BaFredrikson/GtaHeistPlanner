param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $Version = "1.0.0",

    [Parameter()]
    [ValidateSet("win-x64")]
    [string] $Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot "artifacts\release"
$packageName = "GtaHeistPlanner-$Version"
$packageRoot = Join-Path $artifactsRoot $packageName
$applicationRoot = Join-Path $packageRoot "app"
$launcherPublishRoot = Join-Path $artifactsRoot "launcher-$Runtime"
$zipPath = Join-Path $artifactsRoot "$packageName.zip"

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

foreach ($generatedPath in @($packageRoot, $launcherPublishRoot, $zipPath)) {
    $fullPath = [System.IO.Path]::GetFullPath($generatedPath)
    if (-not $fullPath.StartsWith([System.IO.Path]::GetFullPath($artifactsRoot), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a path outside the release artifacts directory: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

dotnet publish (Join-Path $repositoryRoot "GtaHeistPlanner.App\GtaHeistPlanner.App.csproj") `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $applicationRoot
if ($LASTEXITCODE -ne 0) { throw "Application publish failed with exit code $LASTEXITCODE." }

dotnet publish (Join-Path $repositoryRoot "GtaHeistPlanner.Launcher\GtaHeistPlanner.Launcher.csproj") `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $launcherPublishRoot
if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed with exit code $LASTEXITCODE." }

$launcherPath = Join-Path $launcherPublishRoot "GtaHeistPlanner.exe"
if (-not (Test-Path -LiteralPath $launcherPath)) {
    throw "Launcher publish did not produce $launcherPath."
}

Copy-Item -LiteralPath $launcherPath -Destination (Join-Path $packageRoot "GtaHeistPlanner.exe")
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release package: $packageRoot"
Write-Host "Release archive: $zipPath"
