# ThermoScanHeaderExtractor

ThermoScanHeaderExtractor is a Windows command-line tool that extracts compact scan-header metadata from Thermo `.raw` files. It writes Snappy-compressed Parquet scan-table files by default, so it can be invoked by external applications or used directly from a command prompt. An optional `--tsv` flag retains a combined tab-separated diagnostic output mode.

> [!WARNING]
> **Proprietary license notice:** ThermoScanHeaderExtractor includes proprietary Thermo
> RawFileReader components. This repository and its release artifacts are not
> open-source. End users must not redistribute the executable, bundled
> dependencies, or release archives. Do not publish a release until the Thermo
> redistribution authorization and applicable legal review are in place.

## License and usage restrictions

ThermoScanHeaderExtractor is governed by the draft proprietary [ThermoScanHeaderExtractor license](LICENSE.txt). It uses Thermo RawFileReader under the [Thermo RawFileReader license](third_party/Thermo.RawFileReader/RawFileReaderLicense.txt), with the original [Word-format license](third_party/Thermo.RawFileReader/RawFileReaderLicense.doc) retained alongside it.

- **No end-user redistribution:** Thermo's terms require end users to be prohibited from redistributing this software to others.
- **Non-commercial use:** Thermo's terms prohibit commercial exploitation without Thermo's prior written consent.
- **Attribution:** `RawFileReader reading tool. Copyright © 2016 by Thermo Fisher Scientific, Inc. All rights reserved.`

The `--agree-to-terms` command displays the embedded Thermo agreement and records acceptance when the user explicitly invokes that command. No typed confirmation is required. It creates a `.thermo-license-accepted` configuration file beside the executable.

The read-only `--thermo-license` command displays the same embedded Thermo agreement without recording acceptance or creating the marker file.

## Requirements

- Windows
- .NET SDK 8.0 or later
- Network access to download the Thermo RawFileReader NuGet packages on a clean checkout

`scripts/build.ps1` downloads any missing packages into the local feed before
restoring. To download them without building, run:

```powershell
.\scripts\get-thermo-packages.ps1
```

Use `-Force` to replace existing package files. The package versions in
`third_party/Thermo.RawFileReader/thermo-package-source.json` must match the
versions in `src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj`.

The package feed expects these files:

- `ThermoFisher.CommonCore.Data.8.0.37.nupkg`
- `ThermoFisher.CommonCore.RawfileReader.8.0.37.nupkg`

The parent `third_party/Thermo.RawFileReader/` directory contains the original Thermo license documents. Release maintainers must retain Thermo's written redistribution authorization with their release records before publishing a release artifact.

## Build

From the repository root, run:

```powershell
.\scripts\build.ps1
```

The script restores from `nuget.config` and publishes a self-contained,
single-file `ThermoScanHeaderExtractor.exe` to `artifacts\publish\win-x64\`. It copies
`LICENSE.txt`, `RawFileReaderLicense.txt`, and `RawFileReaderLicense.doc` to
that output directory.

Useful options:

```powershell
.\scripts\build.ps1 -Configuration Debug
.\scripts\build.ps1 -Runtime win-x64 -OutputDirectory .\artifacts\release
```

To build directly with the .NET CLI:

```powershell
dotnet restore .\src\ThermoScanHeaderExtractor\ThermoScanHeaderExtractor.csproj --configfile .\nuget.config --runtime win-x64
dotnet publish .\src\ThermoScanHeaderExtractor\ThermoScanHeaderExtractor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\publish\win-x64
```

## CLI usage

```text
ThermoScanHeaderExtractor.exe --agree-to-terms
ThermoScanHeaderExtractor.exe --thermo-license
ThermoScanHeaderExtractor.exe <input.raw> [--tsv] [--high-precision] [--ms1] [--msn]
							  [-o output-directory] [--name output-name]
ThermoScanHeaderExtractor.exe help
```

Before extracting data from a new executable location, run `ThermoScanHeaderExtractor.exe --agree-to-terms` and read the displayed Thermo agreement. Invoking this explicit command records acceptance in `.thermo-license-accepted` beside the executable. No typed confirmation is required. Without that file, extraction exits with an explanation of the requirement.

To only display the Thermo agreement without accepting it, run
`ThermoScanHeaderExtractor.exe --thermo-license`. This command never creates or changes
`.thermo-license-accepted`.

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
├── LICENSE.txt                  # Draft ThermoScanHeaderExtractor proprietary license
├── ThermoScanHeaderExtractor.sln # Standalone solution
├── scripts/build.ps1            # Reproducible Windows publish script
├── src/ThermoScanHeaderExtractor/ # Console application
│   ├── LicenseAcceptance.cs
│   ├── Program.cs
│   ├── ThermoScanHeaderExtractor.csproj
│   └── ScanHeaderExtractor.cs
├── third_party/Thermo.RawFileReader/
│   ├── packages/                # Thermo NuGet packages for local restore
│   ├── RawFileReaderLicense.txt # Thermo license embedded into the executable
│   └── RawFileReaderLicense.doc # Original Thermo license document
├── nuget.config
└── README.md
```