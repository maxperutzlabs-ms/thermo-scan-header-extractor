[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src\ThermoScanHeaderExtractor\ThermoScanHeaderExtractor.csproj"
$nugetConfig = Join-Path $repositoryRoot "nuget.config"
$thermoLicenseText = Join-Path $repositoryRoot "third_party\Thermo.RawFileReader\RawFileReaderLicense.txt"
$thermoLicenseDocument = Join-Path $repositoryRoot "third_party\Thermo.RawFileReader\RawFileReaderLicense.doc"
$productLicense = Join-Path $repositoryRoot "LICENSE.txt"
$downloadScript = Join-Path $PSScriptRoot "get-thermo-packages.ps1"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\publish\$Runtime"
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

$executable = Join-Path $OutputDirectory "ThermoScanHeaderExtractor.exe"
if (-not (Test-Path $executable -PathType Leaf)) {
    throw "Expected executable was not produced: $executable"
}

Copy-Item $thermoLicenseText, $thermoLicenseDocument, $productLicense -Destination $OutputDirectory -Force
Write-Host "Created $executable"