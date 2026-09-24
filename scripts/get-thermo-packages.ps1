[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$configFile = Join-Path `
    $repositoryRoot `
    "third_party\Thermo.RawFileReader\thermo-package-source.json"

$packagesDirectory = Join-Path `
    $repositoryRoot `
    "third_party\Thermo.RawFileReader\packages"

if (-not (Test-Path $configFile -PathType Leaf)) {
    throw "Thermo package configuration not found: $configFile"
}

Write-Host "Reading Thermo package configuration..."

$config = Get-Content $configFile -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($config.repository)) {
    throw "The Thermo package configuration does not specify a repository."
}

if ([string]::IsNullOrWhiteSpace($config.commit)) {
    throw "The Thermo package configuration does not specify a commit."
}

if ($null -eq $config.packages -or $config.packages.Count -eq 0) {
    throw "The Thermo package configuration does not specify any packages."
}

New-Item -ItemType Directory -Force -Path $packagesDirectory | Out-Null

Write-Host "Thermo repository: $($config.repository)"
Write-Host "Thermo commit:     $($config.commit)"
Write-Host ""

foreach ($package in $config.packages) {
    $fileName = Split-Path $package -Leaf
    $destination = Join-Path $packagesDirectory $fileName
    $partialDestination = "$destination.partial"

    if ((Test-Path $destination -PathType Leaf) -and -not $Force) {
        Write-Host "Skipped $fileName (already exists)."
        continue
    }

    $url = "https://raw.githubusercontent.com/$($config.repository)/$($config.commit)/$package"

    Write-Host "Downloading $fileName..."

    try {
        if (Test-Path $partialDestination) {
            Remove-Item $partialDestination -Force
        }

        Invoke-WebRequest `
            -Uri $url `
            -OutFile $partialDestination `
            -ErrorAction Stop

        if (-not (Test-Path $partialDestination -PathType Leaf)) {
            throw "Download completed but the temporary file was not created: $partialDestination"
        }

        Move-Item $partialDestination $destination -Force
    }
    catch {
        if (Test-Path $partialDestination) {
            Remove-Item $partialDestination -Force
        }

        throw "Failed to download Thermo package '$package' from '$url'. $($_.Exception.Message)"
    }

    if (-not (Test-Path $destination -PathType Leaf)) {
        throw "Download completed but the expected file was not created: $destination"
    }

    Write-Host "  -> downloaded to $destination"
}

Write-Host ""
Write-Host "Thermo packages downloaded successfully."