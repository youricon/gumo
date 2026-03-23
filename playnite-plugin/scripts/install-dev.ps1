param(
    [string]$Configuration = "Debug",
    [string]$ProjectRoot = ".\\src\\Gumo.Playnite",
    [string]$PlayniteExtensionsRoot = "$env:APPDATA\\Playnite\\Extensions\\Gumo"
)

$ErrorActionPreference = "Stop"

$buildDir = Join-Path $ProjectRoot "bin\\$Configuration"

if (-not (Test-Path $buildDir)) {
    throw "Expected build output directory was not found: $buildDir"
}

New-Item -ItemType Directory -Force -Path $PlayniteExtensionsRoot | Out-Null
Copy-Item -Path (Join-Path $buildDir "*") -Destination $PlayniteExtensionsRoot -Recurse -Force

Write-Host "Copied development build to $PlayniteExtensionsRoot"
