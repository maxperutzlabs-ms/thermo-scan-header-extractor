# ThermoScanHeaderExtractor

ThermoScanHeaderExtractor is a Windows command-line tool that extracts compact scan-header metadata from Thermo `.raw` files. It writes Snappy-compressed Parquet scan-table files by default, so it can be invoked by external applications or used directly from a command prompt. An optional `--tsv` flag retains a combined tab-separated diagnostic output mode.

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

- Windows
- .NET SDK 8.0 or later
- PowerShell (the scripts in `scripts/` are PowerShell scripts) and Git
- Network access on the first build, to download the Thermo RawFileReader NuGet packages

## Thermo RawFileReader packages

The Thermo NuGet packages are not stored in this repository. They are downloaded from
Thermo's public GitHub repository (`thermofisherlsms/RawFileReader`) at a pinned commit
into `third_party/Thermo.RawFileReader/packages/`, which `nuget.config` uses as a local
feed. The repository, commit, and package paths are defined in
`third_party/Thermo.RawFileReader/thermo-package-source.json`.

- `scripts/build.ps1` runs the download automatically. Packages that already exist in
  the local feed are skipped, so a normal build needs no manual setup.
- To download the packages without building, run: `.\scripts\get-thermo-packages.ps1`
- Run `.\scripts\get-thermo-packages.ps1 -Force` to replace existing package files.
- If you build with the .NET CLI directly (see below), run the download script first.
  Otherwise `dotnet restore` cannot find the Thermo packages.

### Updating the Thermo version

1. In `thermo-package-source.json`, update the package paths (and the commit, if needed).
2. In `src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj`, update the matching package versions. The versions in both files must agree, or restore fails.
3. Run `.\scripts\get-thermo-packages.ps1` (or simply build). Old `.nupkg` files from the previous version can be deleted from the `packages/` folder.

## Build

From the repository root, run:

```powershell
.\scripts\build.ps1
```

The script downloads any missing Thermo packages, restores from `nuget.config`, and
publishes a self-contained, single-file `ThermoScanHeaderExtractor.exe` to
`artifacts\publish\win-x64\`. It copies `LICENSE.txt`, `RawFileReaderLicense.txt`, and
`RawFileReaderLicense.doc` to that output directory.

Useful options:

```powershell
.\scripts\build.ps1 -Configuration Debug
.\scripts\build.ps1 -Runtime win-x64 -OutputDirectory .\artifacts\release
```

To build directly with the .NET CLI (after running `.\scripts\get-thermo-packages.ps1`):

```powershell
dotnet restore .\src\ThermoScanHeaderExtractor\ThermoScanHeaderExtractor.csproj --configfile .\nuget.config --runtime win-x64
dotnet publish .\src\ThermoScanHeaderExtractor\ThermoScanHeaderExtractor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish\win-x64
```

## CLI usage

```text
ThermoScanHeaderExtractor.exe --agree-to-terms
ThermoScanHeaderExtractor.exe --thermo-license
ThermoScanHeaderExtractor.exe --version
ThermoScanHeaderExtractor.exe <input.raw> [--tsv] [--high-precision] [--ms1] [--msn]
							  [-o output-directory] [--name output-name]
ThermoScanHeaderExtractor.exe help
```

Before extracting data from a new executable location, run `ThermoScanHeaderExtractor.exe --agree-to-terms` and read the displayed Thermo agreement. Invoking this explicit command records acceptance in `.thermo-license-accepted` beside the executable. No typed confirmation is required. Without that file, extraction exits with an explanation of the requirement.

To only display the Thermo agreement without accepting it, run
`ThermoScanHeaderExtractor.exe --thermo-license`. This command never creates or changes
`.thermo-license-accepted`.

To display only the current version, run `ThermoScanHeaderExtractor.exe --version`.
This command does not require license acceptance and prints a single version line, for
example `0.1.0`.

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
ThermoScanHeaderExtractor.exe --version
ThermoScanHeaderExtractor.exe sample.raw
ThermoScanHeaderExtractor.exe sample.raw -o .\reports
ThermoScanHeaderExtractor.exe sample.raw -o .\reports --name report.raw
ThermoScanHeaderExtractor.exe sample.raw --high-precision -o .\reports
ThermoScanHeaderExtractor.exe sample.raw --ms1 -o .\reports
ThermoScanHeaderExtractor.exe sample.raw --tsv --msn -o .\reports
```

## Repository layout

```text
.
├── ThermoScanHeaderExtractor.sln        # Standalone solution
├── nuget.config                         # NuGet sources, incl. the local Thermo packages folder
├── LICENSE.txt                          # License file copied into the build output
├── scripts/
│   ├── get-thermo-packages.ps1          # Downloads the Thermo NuGet packages
│   └── build.ps1                        # Reproducible Windows publish script
├── src/ThermoScanHeaderExtractor/       # Console application
├── third_party/Thermo.RawFileReader/
│   ├── thermo-package-source.json       # Pinned upstream repository, commit and package paths
│   ├── packages/                        # Downloaded .nupkg files (git-ignored)
│   ├── RawFileReaderLicense.txt         # Thermo license text
│   └── RawFileReaderLicense.doc         # Original Thermo license document
├── artifacts/publish/<runtime>/         # Build output (git-ignored, created by `build.ps1`)
└── README.md
```
