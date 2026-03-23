[CmdletBinding(PositionalBinding = $false)]
param(
    [string]$Configuration = "Release",
    [string]$ProjectPath = ".\\src\\Gumo.Playnite\\Gumo.Playnite.csproj",
    [string]$OutputRoot = ".\\artifacts",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$pluginRoot = Split-Path -Parent $scriptRoot
$defaultProjectPath = ".\\src\\Gumo.Playnite\\Gumo.Playnite.csproj"

function Resolve-MSBuildPath {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $installationPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -property installationPath
        if ($LASTEXITCODE -eq 0 -and $installationPath) {
            $candidate = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $fallbacks = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe")
    )

    foreach ($candidate in $fallbacks) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "MSBuild.exe was not found. Install Visual Studio 2022 or Build Tools with the MSBuild component."
}

function Get-ProjectPropertyValue {
    param(
        [string]$ProjectFile,
        [string]$PropertyName
    )

    [xml]$projectXml = Get-Content $ProjectFile
    foreach ($propertyGroup in $projectXml.Project.PropertyGroup) {
        $property = $propertyGroup.$PropertyName
        if ($property -and $property.'#text') {
            return $property.'#text'.Trim()
        }
        if ($property -is [string] -and $property.Trim()) {
            return $property.Trim()
        }
    }

    return $null
}

function Resolve-BuildOutputDirectory {
    param(
        [string]$ProjectFile,
        [string]$BuildConfiguration,
        [string]$ModuleName
    )

    $projectDir = Split-Path -Parent $ProjectFile
    $baseDir = Join-Path $projectDir "bin\\$BuildConfiguration"
    $candidates = @()

    if (Test-Path $baseDir) {
        $candidates += $baseDir
    }

    $targetFramework = Get-ProjectPropertyValue -ProjectFile $ProjectFile -PropertyName "TargetFramework"
    if ($targetFramework) {
        $frameworkDir = Join-Path $baseDir $targetFramework
        if (Test-Path $frameworkDir) {
            $candidates = @($frameworkDir) + $candidates
        }
    }

    if (Test-Path $baseDir) {
        $nestedDirs = Get-ChildItem -Path $baseDir -Directory -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
        if ($nestedDirs) {
            $candidates += $nestedDirs
        }
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate $ModuleName)) {
            return $candidate
        }
    }

    throw "Expected plugin module was not found in build output: $ModuleName"
}

# Be tolerant of invocations like:
#   .\scripts\package.ps1 --Configuration Release
# where PowerShell may bind "Release" into the first positional string parameter.
if ($ProjectPath -and $ProjectPath -notlike "*.csproj" -and $Configuration -eq "Release") {
    $Configuration = $ProjectPath
    $ProjectPath = $defaultProjectPath
}

$projectPath = Resolve-Path (Join-Path $pluginRoot $ProjectPath)
$projectDir = Split-Path -Parent $projectPath
$outputRoot = Join-Path $pluginRoot $OutputRoot
$manifestPath = Join-Path $projectDir "extension.yaml"

if (-not $SkipBuild) {
    Write-Host "Building Gumo Playnite plugin ($Configuration)..."
    $msbuildPath = Resolve-MSBuildPath
    & $msbuildPath $projectPath /t:Build /p:Configuration=$Configuration
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

$buildDir = Resolve-BuildOutputDirectory -ProjectFile $projectPath -BuildConfiguration $Configuration -ModuleName $manifest.Module

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
