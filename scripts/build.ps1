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
$thermoLicenseText = Join-Path $repositoryRoot "third_party/Thermo.RawFileReader/RawFileReader-DistributionLicense.md"
$thermoLicenseDocument = Join-Path $repositoryRoot "third_party/Thermo.RawFileReader/RawFileReader-DistributionLicense.doc"
$productLicense = Join-Path $repositoryRoot "LICENSE.txt"
$thirdPartyNotices = Join-Path $repositoryRoot "THIRD-PARTY-NOTICES.txt"
# Must match <TargetFramework> in the project file.
$targetFramework = "net8.0"
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

Copy-Item $thermoLicenseText, $thermoLicenseDocument, $productLicense, $thirdPartyNotices -Destination $OutputDirectory -Force

# THIRD-PARTY-NOTICES.txt must list exactly the NuGet packages in the executable, so that an
# update that adds, removes or changes a package fails the build instead of shipping wrong notices.
# The Thermo packages are covered by the Thermo license instead.
$depsFile = Join-Path $repositoryRoot "src/ThermoScanHeaderExtractor/bin/$Configuration/$targetFramework/$Runtime/ThermoScanHeaderExtractor.deps.json"
$deps = Get-Content -Raw $depsFile | ConvertFrom-Json
$depsTarget = $deps.targets.PSObject.Properties | Where-Object { $_.Name.EndsWith("/$Runtime") } | Select-Object -First 1
if ($null -eq $depsTarget) {
    throw "No target for '$Runtime' in $depsFile."
}
$bundledPackages = @(
    $depsTarget.Value.PSObject.Properties |
        Where-Object {
            $deps.libraries.($_.Name).type -eq "package" -and
            ($null -ne $_.Value.runtime -or $null -ne $_.Value.native) -and
            -not $_.Name.StartsWith("ThermoFisher.CommonCore.")
        } |
        ForEach-Object { $_.Name } |
        Sort-Object
)
$listedPackages = @(
    Get-Content $thirdPartyNotices |
        Where-Object { $_ -match '^Package: https://www\.nuget\.org/packages/([^/]+)/([^/\s]+)$' } |
        ForEach-Object { "$($Matches[1])/$($Matches[2])" } |
        Sort-Object
)
$missingPackages = @($bundledPackages | Where-Object { $_ -notin $listedPackages })
$extraPackages = @($listedPackages | Where-Object { $_ -notin $bundledPackages })
if ($missingPackages.Count -ne 0 -or $extraPackages.Count -ne 0) {
    throw "THIRD-PARTY-NOTICES.txt does not match the packages in the executable. Not listed: $($missingPackages -join ', '). Listed but not included: $($extraPackages -join ', ')."
}

# The .NET runtime is part of the self-contained executable. Copy its license and notices
# from the runtime pack that was used, so that they match the bundled runtime version.
$assets = Get-Content -Raw (Join-Path $repositoryRoot "src/ThermoScanHeaderExtractor/obj/project.assets.json") | ConvertFrom-Json
$runtimePackName = "Microsoft.NETCore.App.Runtime.$Runtime"
$runtimePack = @($assets.project.frameworks.$targetFramework.downloadDependencies | Where-Object { $_.name -eq $runtimePackName })
# The restore output records the exact version as a range such as "[8.0.34, 8.0.34]".
$runtimePackVersion = if ($runtimePack.Count -eq 1 -and $runtimePack[0].version -match '^\[([^,\]]+), *\1\]$') { $Matches[1] }
if (-not $runtimePackVersion) {
    throw "Could not determine the exact version of $runtimePackName from the restore output."
}
$runtimePackDirectory = Join-Path $assets.project.restore.packagesPath "$($runtimePackName.ToLowerInvariant())/$runtimePackVersion"
foreach ($file in @(
    @{ Source = "LICENSE.TXT"; Destination = "dotnet-LICENSE.txt" },
    @{ Source = "THIRD-PARTY-NOTICES.TXT"; Destination = "dotnet-THIRD-PARTY-NOTICES.txt" }
)) {
    $sourcePath = Join-Path $runtimePackDirectory $file.Source
    if (-not (Test-Path $sourcePath -PathType Leaf)) {
        throw "The .NET runtime notice file was not found: $sourcePath"
    }
    Copy-Item $sourcePath -Destination (Join-Path $OutputDirectory $file.Destination) -Force
}

Write-Host "Created $executable"