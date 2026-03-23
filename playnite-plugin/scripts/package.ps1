param(
    [string]$Configuration = "Release",
    [string]$ProjectPath = ".\\src\\Gumo.Playnite\\Gumo.Playnite.csproj",
    [string]$OutputRoot = ".\\artifacts"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Gumo Playnite plugin ($Configuration)..."
msbuild $ProjectPath /t:Build /p:Configuration=$Configuration

$projectDir = Split-Path -Parent $ProjectPath
$buildDir = Join-Path $projectDir "bin\\$Configuration"

if (-not (Test-Path $buildDir)) {
    throw "Expected build output directory was not found: $buildDir"
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

Write-Host "Build output is available at: $buildDir"
Write-Host "Packaging into .pext is not automated yet. This script is the placeholder entrypoint for Task 10."
