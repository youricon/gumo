param(
    [string]$Configuration = "Release",
    [string]$ProjectPath = ".\\src\\Gumo.Playnite\\Gumo.Playnite.csproj",
    [string]$OutputRoot = ".\\artifacts",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginRoot = Split-Path -Parent $scriptRoot
$projectPath = Resolve-Path (Join-Path $pluginRoot $ProjectPath)
$projectDir = Split-Path -Parent $projectPath
$buildDir = Join-Path $projectDir "bin\\$Configuration"
$outputRoot = Join-Path $pluginRoot $OutputRoot
$manifestPath = Join-Path $projectDir "extension.yaml"

if (-not $SkipBuild) {
    Write-Host "Building Gumo Playnite plugin ($Configuration)..."
    msbuild $projectPath /t:Build /p:Configuration=$Configuration
}

if (-not (Test-Path $buildDir)) {
    throw "Expected build output directory was not found: $buildDir"
}

if (-not (Test-Path $manifestPath)) {
    throw "Extension manifest was not found: $manifestPath"
}

$manifest = @{
    Id = $null
    Name = $null
    Version = $null
    Module = $null
}

Get-Content $manifestPath | ForEach-Object {
    if ($_ -match '^Id:\s*(.+)$') {
        $manifest.Id = $Matches[1].Trim()
    } elseif ($_ -match '^Name:\s*(.+)$') {
        $manifest.Name = $Matches[1].Trim()
    } elseif ($_ -match '^Version:\s*(.+)$') {
        $manifest.Version = $Matches[1].Trim()
    } elseif ($_ -match '^Module:\s*(.+)$') {
        $manifest.Module = $Matches[1].Trim()
    }
}

if (-not $manifest.Id -or -not $manifest.Version -or -not $manifest.Module) {
    throw "extension.yaml is missing one of the required fields: Id, Version, Module"
}

if (-not (Test-Path (Join-Path $buildDir $manifest.Module))) {
    throw "Expected plugin module was not found in build output: $($manifest.Module)"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$safeVersion = $manifest.Version -replace '[^0-9A-Za-z._-]', '_'
$artifactBaseName = "$($manifest.Id)-$safeVersion"
$stagingDir = Join-Path $outputRoot $artifactBaseName
$artifactPath = Join-Path $outputRoot "$artifactBaseName.pext"

if (Test-Path $stagingDir) {
    Remove-Item -Recurse -Force $stagingDir
}

if (Test-Path $artifactPath) {
    Remove-Item -Force $artifactPath
}

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

$includePatterns = @("*.dll", "*.exe", "*.yaml", "*.json", "*.config")
$excludeNames = @("PlayniteSDK.xml", "Gumo.Playnite.pdb", "Gumo.Playnite.xml")

foreach ($pattern in $includePatterns) {
    Get-ChildItem -Path $buildDir -Filter $pattern -File | Where-Object {
        $excludeNames -notcontains $_.Name
    } | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination (Join-Path $stagingDir $_.Name) -Force
    }
}

if (-not (Test-Path (Join-Path $stagingDir "extension.yaml"))) {
    Copy-Item -Path $manifestPath -Destination (Join-Path $stagingDir "extension.yaml") -Force
}

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $artifactPath -CompressionLevel Optimal

$artifactSize = (Get-Item $artifactPath).Length

Write-Host "Packaged Playnite extension:"
Write-Host "  Artifact: $artifactPath"
Write-Host "  Version:  $($manifest.Version)"
Write-Host "  Module:   $($manifest.Module)"
Write-Host "  Size:     $artifactSize bytes"

Write-Host ""
Write-Host "Next step:"
Write-Host "  Validate the .pext in a clean Playnite environment before cutting a release."
