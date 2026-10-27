# ThermoScanHeaderExtractor

ThermoScanHeaderExtractor is a cross-platform command-line tool that extracts compact scan-header metadata from Thermo `.raw` files on Windows, Linux, and macOS. It writes Snappy-compressed Parquet scan-table files by default, so it can be invoked by external applications or used directly from a command prompt. An optional `--tsv` flag writes the same files as tab-separated text for diagnostics.

> [!WARNING]
> **Proprietary license notice:** ThermoScanHeaderExtractor includes proprietary Thermo
> RawFileReader components. This repository and its release artifacts are not
> open-source. End users must not redistribute the executable, bundled
> dependencies, or release archives. Do not publish a release until the Thermo
> redistribution authorization and applicable legal review are in place.

## Overview

- [License and usage restrictions](#license-and-usage-restrictions)
- [Requirements](#requirements)
- [Thermo RawFileReader packages](#thermo-rawfilereader-packages)
- [Build](#build)
- [CLI usage](#cli-usage)
- [Repository layout](#repository-layout)

## License and usage restrictions

ThermoScanHeaderExtractor is licensed under the Apache License 2.0. It depends on Thermo RawFileReader under the terms of the  [Thermo RawFileReader license](third_party/Thermo.RawFileReader/RawFileReaderLicense.txt) (the original [Word document](third_party/Thermo.RawFileReader/RawFileReaderLicense.doc) is also retained for reference).

- **No end-user redistribution:** Thermo's terms require end users to be prohibited from redistributing this software to others.
- **Non-commercial use:** Thermo's terms prohibit commercial exploitation without Thermo's prior written consent.
- **Attribution:** `RawFileReader reading tool. Copyright © 2016 by Thermo Fisher Scientific, Inc. All rights reserved.`

The `--agree-to-terms` command displays the embedded Thermo agreement and records acceptance when the user explicitly invokes that command. No typed confirmation is required. It creates a `.thermo-license-accepted` configuration file beside the executable.

The read-only `--thermo-license` command displays the same embedded Thermo agreement without recording acceptance or creating the marker file.

Release maintainers must retain Thermo's written redistribution authorization with their release records before publishing a release artifact.

## Requirements

- Windows, Linux, or macOS (x64 and arm64)
- .NET SDK 8.0 and PowerShell 7 or later to build from source
- Network access on the first build, to download the Thermo RawFileReader NuGet packages

The build produces a self-contained executable, so end users do not need to install .NET. If you build or run the framework-dependent DLL directly, install the .NET 8 runtime on the target machine. Linux hosts must also have the native libraries required by .NET 8 (including glibc, ICU, OpenSSL, and zlib; exact package names vary by distribution). For macOS, use a .NET 8-supported version (macOS 14 or later) and a matching x64/arm64 build. Mono and Thermo's legacy MSFileReader are not required.

## Thermo RawFileReader packages

The Thermo NuGet packages are not stored in this repository. They are downloaded from Thermo's public GitHub repository (`thermofisherlsms/RawFileReader`) at a pinned commit into `third_party/Thermo.RawFileReader/packages/`, which `nuget.config` uses as a local feed. The repository, commit, and package paths are defined in `third_party/Thermo.RawFileReader/thermo-package-source.json`.

- `scripts/build.ps1` runs the download automatically. Packages that already exist in the local feed are skipped, so a normal build needs no manual setup.
- To download the packages without building, run: `pwsh ./scripts/get-thermo-packages.ps1`
- Run `pwsh ./scripts/get-thermo-packages.ps1 -Force` to replace existing package files.
- If you build with the .NET CLI directly (see [Build](#build)), run the download script first. Otherwise `dotnet restore` cannot find the Thermo packages.

### Updating the Thermo version

1. In `thermo-package-source.json`, update the package paths (and the commit, if needed).
2. In `src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj`, update the matching package versions. The versions in both files must agree, or restore fails.
3. Run `pwsh ./scripts/get-thermo-packages.ps1` (or simply build). Old `.nupkg` files from the previous version can be deleted from the `packages/` folder.

## Build

From the repository root, run the following with PowerShell 7:

```sh
pwsh ./scripts/build.ps1
```

The script downloads any missing Thermo packages, restores from `nuget.config`, and publishes a self-contained, single-file executable for the current OS and architecture to `artifacts/publish/<runtime-id>/`. Windows uses `artifacts/publish/win-x64/ThermoScanHeaderExtractor.exe`; Linux and macOS use the extensionless `ThermoScanHeaderExtractor` apphost. It copies `LICENSE.txt`, `RawFileReaderLicense.txt`, and `RawFileReaderLicense.doc` to that output directory.

Useful options:

```sh
pwsh ./scripts/build.ps1 -Configuration Debug
pwsh ./scripts/build.ps1 -Runtime linux-x64 -OutputDirectory ./artifacts/release
pwsh ./scripts/build.ps1 -Runtime linux-arm64
```

Supported runtime IDs are `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`. A cross-published executable must be run on a matching OS and architecture. Windows and Linux runtimes can be built on any OS, but `osx-*` runtimes must be built on macOS, because only a macOS build host code-signs the executable and Apple Silicon Macs refuse to launch unsigned executables. The script stops with an error if an `osx-*` runtime is requested on another OS.

To build with the .NET CLI directly, run `pwsh ./scripts/get-thermo-packages.ps1` first, then replace `<runtime-id>` with one of the IDs above:

```sh
dotnet restore ./src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj --configfile ./nuget.config --runtime <runtime-id>
dotnet publish ./src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj -c Release -r <runtime-id> --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./artifacts/publish/<runtime-id>
```

## CLI usage

On Windows, invoke `ThermoScanHeaderExtractor.exe`; on Linux and macOS, invoke `./ThermoScanHeaderExtractor`. The commands below use the platform-neutral command name:

```text
ThermoScanHeaderExtractor --agree-to-terms
ThermoScanHeaderExtractor --thermo-license
ThermoScanHeaderExtractor --version
ThermoScanHeaderExtractor <input.raw> [--tsv] [--high-precision] [--ms1] [--msn]
                         [-o output-directory] [--name output-name]
ThermoScanHeaderExtractor help
```

Before extracting data from a new application location, run `ThermoScanHeaderExtractor --agree-to-terms` and read the displayed Thermo agreement. Invoking this explicit command records acceptance in `.thermo-license-accepted` beside the application. No typed confirmation is required. Without that file, extraction exits with an explanation of the requirement.

To only display the Thermo agreement without accepting it, run `ThermoScanHeaderExtractor --thermo-license`. This command never creates or changes `.thermo-license-accepted`.

To display only the current version, run `ThermoScanHeaderExtractor --version`. This command does not require license acceptance and prints a single version line, for example `0.1.0`.

The `help` subcommand, `--help`, and `-h` print command usage, the current `major.minor.micro` version, and the required Thermo attribution. By default, extraction writes the following sibling files beside the input (or in the directory given by `-o`):

```text
<input-name>.ms1-trace.parquet
<input-name>.msn-trace.parquet
```

Use `--name <name>` to replace `<input-name>` in both output names. The supplied name is used literally, so `--name report.raw` creates `report.raw.ms1-trace.parquet` and `report.raw.msn-trace.parquet`. Names must be valid file names and cannot contain path separators; use `-o` to select the output directory.

Use `--ms1` or `--msn` to write only one level. With neither flag, or with both flags, the tool writes both available levels. A single-level request for a level absent from the source exits with an error before writing files. The MSn file is otherwise not produced for an MS1-only acquisition. The two files have the required columns `scan_id`, `retention_time_sec`, and `ms_level`, plus the implemented optional columns `total_ion_current` and `base_peak_intensity`. `faims_cv` is included as nullable float32 only when the input contains non-zero FAIMS compensation voltages. `injection_time_ms` and all reserved columns are not currently emitted.

`scan_id` is the native Thermo scan number and each row represents exactly one spectrum. Retention time is expressed in seconds. Parquet footer metadata records schema version, generator, Thermo vendor, file level, precision mode, spectrum-level row granularity, source filename, source ID when valid instrument metadata is available, extraction timestamp, acquisition start time, and the vendor-recorded acquisition path. `source_filename` is the original RAW filename, including its extension, and is independent of any `--name` output override. `source_id` is composed as `<instrument-serial>_<creation-time>`, with the creation time formatted as `yyyyMMddHHmmss`, and is omitted when the instrument serial number is unavailable. `acquisition_start_time` is ISO-8601 UTC and is shared by both sibling files. `acquisition_path` is the vendor-recorded folder where the RAW file was acquired, rather than its current location. TSV output does not carry this metadata.

The default `low` precision mode stores `total_ion_current`, `base_peak_intensity`, and `retention_time_sec` as float32. `--high-precision` writes those three columns as float64. `faims_cv` is always float32. The `--tsv` flag writes the same selected MS1/MSn sibling files with a `.tsv` extension instead of `.parquet`. TSV files have the same columns and units, but do not carry Parquet schema metadata.

Examples:

```powershell
ThermoScanHeaderExtractor --version
ThermoScanHeaderExtractor sample.raw
ThermoScanHeaderExtractor sample.raw -o ./reports
ThermoScanHeaderExtractor sample.raw -o ./reports --name report.raw
ThermoScanHeaderExtractor sample.raw --high-precision -o ./reports
ThermoScanHeaderExtractor sample.raw --ms1 -o ./reports
ThermoScanHeaderExtractor sample.raw --tsv --msn -o ./reports
```

## Repository layout

```text
.
├── ThermoScanHeaderExtractor.sln        # Standalone solution
├── nuget.config                         # NuGet sources, incl. the local Thermo packages folder
├── LICENSE.txt                          # License file copied into the build output
├── .github/workflows/                   # CI (build + sample extraction per runtime) and release builds
├── scripts/
│   ├── get-thermo-packages.ps1          # Downloads the Thermo NuGet packages
│   └── build.ps1                        # Cross-platform publish script (PowerShell 7)
├── src/ThermoScanHeaderExtractor/       # Console application
├── tests/data/thermo/                   # Small Thermo RAW fixture used by CI
├── third_party/Thermo.RawFileReader/
│   ├── thermo-package-source.json       # Pinned upstream repository, commit and package paths
│   ├── packages/                        # Downloaded .nupkg files (git-ignored)
│   ├── RawFileReaderLicense.txt         # Thermo license text
│   └── RawFileReaderLicense.doc         # Original Thermo license document
├── artifacts/publish/<runtime>/         # Build output (git-ignored, created by `build.ps1`)
└── README.md
```
