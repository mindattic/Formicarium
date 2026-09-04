<#
.SYNOPSIS
    Builds the nanoFramework controller firmware without installing the Visual Studio extension.

.DESCRIPTION
    An .nfproj needs three things that a plain SDK install does not provide:

      1. The NFProjectSystem MSBuild props/targets and the two build-task assemblies
         (metadata processor, binary generator). These normally arrive with the Visual Studio
         extension, but they also ship inside its VSIX under "$MSBuild/nanoFramework/v1.0/",
         and a VSIX is just a zip. This script downloads that release asset and extracts the
         folder, which is exactly the layout a real install produces.

      2. packages.config restore into a "packages" folder. nanoFramework predates PackageReference,
         so assemblies are referenced with explicit HintPaths.

      3. MSBuild proper - not "dotnet build". The project targets netnano1.0 and the nanoFramework
         build tasks are .NET Framework assemblies.

    Everything lands under firmware/.build/, which is gitignored. Nothing is installed
    machine-wide and no Visual Studio extension is registered.

.PARAMETER Configuration
    Release (default) or Debug.

.PARAMETER Clean
    Re-download the build components even if they are already present.

.EXAMPLE
    ./build-firmware.ps1
    ./build-firmware.ps1 -Configuration Debug -Clean
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

# Pinned deliberately. The build components and the project system evolve together, so an
# unpinned "latest" turns an unrelated upstream release into a mystery build break.
$ExtensionRelease = "v2022.14.1.13"
$VsixName = "nanoFramework.Tools.VS2022.Extension.vsix"

$firmwareRoot = $PSScriptRoot
$buildRoot = Join-Path $firmwareRoot ".build"
$targetsRoot = Join-Path $buildRoot "nanoFramework"
$targetsDir = Join-Path $targetsRoot "v1.0"
$packagesDir = Join-Path $firmwareRoot "packages"
$projectDir = Join-Path $firmwareRoot "Formicarium.Controller"
$project = Join-Path $projectDir "Formicarium.Controller.nfproj"

function Write-Step($text) {
    Write-Host ""
    Write-Host "==> $text" -ForegroundColor Cyan
}

if ($Clean -and (Test-Path $buildRoot)) {
    Remove-Item $buildRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null

# --- 1. build components --------------------------------------------------------
if (-not (Test-Path (Join-Path $targetsDir "NFProjectSystem.CSharp.targets"))) {
    Write-Step "Fetching nanoFramework build components ($ExtensionRelease)"

    $vsix = Join-Path $buildRoot $VsixName
    $url = "https://github.com/nanoframework/nf-Visual-Studio-extension/releases/download/$ExtensionRelease/$VsixName"

    Invoke-WebRequest -Uri $url -OutFile $vsix -UseBasicParsing

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($vsix)
    try {
        $prefix = '$MSBuild/nanoFramework/v1.0/'
        $count = 0
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.StartsWith($prefix) -and $entry.Name) {
                $relative = $entry.FullName.Substring($prefix.Length)
                $destination = Join-Path $targetsDir $relative
                New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
                $count++
            }
        }
        Write-Host "    extracted $count files"
    }
    finally {
        $zip.Dispose()
    }
}
else {
    Write-Host "==> Build components already present" -ForegroundColor DarkGray
}

# --- 2. nuget.exe ---------------------------------------------------------------
$nuget = Join-Path $buildRoot "nuget.exe"
if (-not (Test-Path $nuget)) {
    Write-Step "Fetching nuget.exe"
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget -UseBasicParsing
}

# --- 3. restore -----------------------------------------------------------------
Write-Step "Restoring packages"
& $nuget restore (Join-Path $projectDir "packages.config") -PackagesDirectory $packagesDir | Out-Null
if ($LASTEXITCODE -ne 0) { throw "package restore failed" }

# --- 4. locate MSBuild ----------------------------------------------------------
Write-Step "Locating MSBuild"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found - Visual Studio or Build Tools is required" }

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
    Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found - install Visual Studio Build Tools" }
Write-Host "    $msbuild"

# --- 5. build -------------------------------------------------------------------
Write-Step "Building $Configuration"

# Two things matter here and both fail silently as "there is no target in the project":
# the path must point at the v1.0 folder itself, not its parent, and it must end in a
# separator, because the project concatenates it directly with the props file name.
$systemPath = $targetsDir.TrimEnd('\', '/') + '\'

& $msbuild $project `
    -nologo `
    -verbosity:minimal `
    -p:Configuration=$Configuration `
    -p:NanoFrameworkProjectSystemPath="$systemPath"

if ($LASTEXITCODE -ne 0) { throw "build failed" }

$output = Join-Path $projectDir "bin\$Configuration"
$image = Join-Path $output "Formicarium.Controller.pe"

Write-Step "Done"
if (Test-Path $image) {
    $size = (Get-Item $image).Length
    Write-Host "    Deployable image: $image ($size bytes)" -ForegroundColor Green
    Write-Host "    Dependency assemblies (.pe): $((Get-ChildItem $output -Filter *.pe).Count)"
    Write-Host ""
    Write-Host "    To flash, with the board attached:" -ForegroundColor DarkGray
    Write-Host "      nanoff --target ESP32_REV0 --serialport COMx --update" -ForegroundColor DarkGray
    Write-Host "      nanoff --target ESP32_REV0 --serialport COMx --deploy --image `"$image`"" -ForegroundColor DarkGray
}
else {
    throw "build reported success but produced no .pe image"
}
