[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "",

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj"
$nugetConfig = Join-Path $repositoryRoot "nuget.config"
$thermoLicenseText = Join-Path $repositoryRoot "third_party/Thermo.RawFileReader/RawFileReaderLicense.txt"
$thermoLicenseDocument = Join-Path $repositoryRoot "third_party/Thermo.RawFileReader/RawFileReaderLicense.doc"
$productLicense = Join-Path $repositoryRoot "LICENSE.txt"
$downloadScript = Join-Path $PSScriptRoot "get-thermo-packages.ps1"

if ([string]::IsNullOrWhiteSpace($Runtime)) {
    $architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
    $platform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        "win"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
        "linux"
    }
    elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
        "osx"
    }
    else {
        throw "Cannot infer a .NET runtime identifier for this operating system."
    }

    $Runtime = "$platform-$architecture"
}

$supportedRuntimes = @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")
if ($Runtime -notin $supportedRuntimes) {
    throw "Unsupported runtime '$Runtime'. Supported runtimes: $($supportedRuntimes -join ', ')."
}

# The .NET SDK only code-signs macOS executables when publishing on macOS, and
# Apple Silicon refuses to launch unsigned executables.
if ($Runtime.StartsWith("osx-", [StringComparison]::OrdinalIgnoreCase) -and
    -not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
    throw "The '$Runtime' runtime must be built on macOS so that the executable is code-signed."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/publish/$Runtime"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Write-Host "Ensuring Thermo packages are available..."
& $downloadScript

Write-Host "Restoring ThermoScanHeaderExtractor..."
& dotnet restore $project --configfile $nugetConfig --runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

Write-Host "Publishing ThermoScanHeaderExtractor to $OutputDirectory..."
& dotnet publish $project `
    --no-restore `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executableName = "ThermoScanHeaderExtractor"
if ($Runtime.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) {
    $executableName += ".exe"
}

$executable = Join-Path $OutputDirectory $executableName
if (-not (Test-Path $executable -PathType Leaf)) {
    throw "Expected executable was not produced: $executable"
}

Copy-Item $thermoLicenseText, $thermoLicenseDocument, $productLicense -Destination $OutputDirectory -Force
Write-Host "Created $executable"