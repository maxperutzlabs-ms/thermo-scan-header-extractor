# ThermoScanHeaderExtractor

ThermoScanHeaderExtractor is a cross-platform command-line tool that extracts compact scan-header metadata from Thermo `.raw` files on Windows, Linux, and macOS. It writes Snappy-compressed Parquet scan-table files by default, which follow the [scan-trace specification](https://github.com/maxperutzlabs-ms/rawtrace-qc/blob/main/docs/scan-trace-spec.md), so it can be invoked by external applications or used directly from a command prompt. An optional `--tsv` flag writes the same files as tab-separated text for diagnostics.

> [!WARNING]
> **Thermo license notice:** Built executables and release archives include the proprietary Thermo RawFileReader library. Using them requires accepting the [Thermo RawFileReader license](#license-and-usage-restrictions). To share the tool, send people the link to the release page instead of a copy.

## Overview

- [License and usage restrictions](#license-and-usage-restrictions)
- [Requirements](#requirements)
- [Thermo RawFileReader packages](#thermo-rawfilereader-packages)
- [Build](#build)
- [CLI usage](#cli-usage)
- [Repository layout](#repository-layout)

## License and usage restrictions

ThermoScanHeaderExtractor is licensed under the Apache License 2.0. It uses the Thermo RawFileReader library, which has its own [Thermo RawFileReader license](third_party/Thermo.RawFileReader/RawFileReader-DistributionLicense.md) (the original [Word document](third_party/Thermo.RawFileReader/RawFileReader-DistributionLicense.doc) is kept for reference). In short:

- **Accept before use:** you must accept the Thermo license before you use the tool, with `--agree-to-terms` (see below).
- **No commercial exploitation:** you may not exploit the software commercially without Thermo's written consent, for example by selling it, charging fees for it, or passing it on for payment.
- **Passing it on:** you may only pass RawFileReader on together with your own software, and only under the conditions in section 3 of the Thermo license. To share the tool, send people the link to the release page instead of a copy.
- **Attribution:** `RawFileReader. Copyright © 2016-2026 by Thermo Fisher Scientific, Inc. All rights reserved.`

This summary does not replace the license text.

The executable also contains the .NET runtime and several open-source libraries, for example OpenMcdf and Parquet.Net. [`THIRD-PARTY-NOTICES.txt`](THIRD-PARTY-NOTICES.txt) lists which license covers which part, with the full license texts. It is included in every release archive, together with the license and notices of the .NET runtime (`dotnet-LICENSE.txt` and `dotnet-THIRD-PARTY-NOTICES.txt`).

The `--agree-to-terms` command displays the embedded Thermo agreement and records acceptance when the user explicitly invokes that command. No typed confirmation is required. Acceptance is recorded in a `.thermo-license-accepted` file, by default in the user's configuration directory (see [CLI usage](#cli-usage)).

The read-only `--thermo-license` command displays the same embedded Thermo agreement without recording acceptance or creating the marker file.

## Requirements

- Windows, Linux, or macOS (x64 and arm64)
- .NET SDK 8.0 and PowerShell 7 or later to build from source
- Network access on the first build, to download the Thermo RawFileReader NuGet packages

The build produces a self-contained executable, so end users do not need to install .NET. If you build or run the framework-dependent DLL directly, install the .NET 8 runtime on the target machine. On Linux, the executable needs a glibc-based distribution with the C and C++ runtime libraries (`libstdc++`, `libgcc_s`) and zlib (`libz.so.1`). It does not need ICU (`libicu`), because it runs in .NET's invariant globalization mode; CI checks that it works in a minimal `ubuntu:24.04` container. For macOS, use a .NET 8-supported version (macOS 14 or later) and a matching x64/arm64 build. Mono and Thermo's legacy MSFileReader are not required.

## Thermo RawFileReader packages

The Thermo NuGet packages are not stored in this repository. They are downloaded from Thermo's public GitHub repository (`thermofisherlsms/RawFileReader`) at a pinned commit into `third_party/Thermo.RawFileReader/packages/`, which `nuget.config` uses as a local feed. `nuget.config` allows the Thermo packages only from this folder, never from nuget.org. The repository, commit, and package paths are defined in `third_party/Thermo.RawFileReader/thermo-package-source.json`.

- `scripts/build.ps1` runs the download automatically. Packages that already exist in the local feed are skipped, so a normal build needs no manual setup.
- To download the packages without building, run: `pwsh ./scripts/get-thermo-packages.ps1`
- Run `pwsh ./scripts/get-thermo-packages.ps1 -Force` to replace existing package files.
- If you build with the .NET CLI directly (see [Build](#build)), run the download script first. Otherwise `dotnet restore` cannot find the Thermo packages.

### Updating the Thermo version

1. In `thermo-package-source.json`, set the commit of the upstream repository that contains the new version, and update the package paths.
2. In `src/ThermoScanHeaderExtractor/ThermoScanHeaderExtractor.csproj`, update the matching package versions. The versions in both files must agree, or restore fails.
3. Run `pwsh ./scripts/get-thermo-packages.ps1` (or simply build). Old `.nupkg` files from the previous version can be deleted from the `packages/` folder.
4. Check whether the new version needs other NuGet packages. If it does, the build stops with an error that names them. Add them to `THIRD-PARTY-NOTICES.txt`, with their license texts from the upstream repository at the matching version.
5. Check whether the license in the upstream repository changed at that commit. If it did, replace `RawFileReader-DistributionLicense.doc` with the upstream file, convert its text to `RawFileReader-DistributionLicense.md`, and update the attribution and license texts in the README, `CHANGELOG.md`, `Program.cs`, `LicenseAcceptance.cs` and `release.yml`. If the upstream file name changed, also update `build.ps1`, the `.csproj`, `release.yml` and `test-build.yml`.

## Build

From the repository root, run the following with PowerShell 7:

```sh
pwsh ./scripts/build.ps1
```

The script downloads any missing Thermo packages, restores from `nuget.config`, and publishes a self-contained, single-file executable for the current OS and architecture to `artifacts/publish/<runtime-id>/`. Windows uses `artifacts/publish/win-x64/ThermoScanHeaderExtractor.exe`; Linux and macOS use the extensionless `ThermoScanHeaderExtractor` apphost. It copies `LICENSE.txt`, `RawFileReader-DistributionLicense.md`, `RawFileReader-DistributionLicense.doc`, and `THIRD-PARTY-NOTICES.txt` to that output directory, and the license and notices of the bundled .NET runtime as `dotnet-LICENSE.txt` and `dotnet-THIRD-PARTY-NOTICES.txt`. The build stops with an error if `THIRD-PARTY-NOTICES.txt` does not list exactly the NuGet packages in the executable (apart from the Thermo packages). This happens, for example, after a package update that adds a dependency. Then update `THIRD-PARTY-NOTICES.txt`.

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
ThermoScanHeaderExtractor --license-status
ThermoScanHeaderExtractor --version
ThermoScanHeaderExtractor <input.raw> [--tsv] [--high-precision] [--ms1] [--msn]
                         [-o output-directory] [--name output-name]
ThermoScanHeaderExtractor help
```

Before extracting data for the first time, run `ThermoScanHeaderExtractor --agree-to-terms` and read the displayed Thermo agreement. Invoking this explicit command records acceptance for the current user in a `.thermo-license-accepted` file. No typed confirmation is required. Without that file, extraction exits with an explanation of the requirement. Acceptance survives tool updates and reinstalls. By default it is per user, so every user account (including service accounts or containers that invoke the tool) must accept once. Missing directories are created automatically. By default, the file is stored in the user's configuration directory, following the layout of Python's `platformdirs`:

| OS | Location |
|---|---|
| Windows | `%LOCALAPPDATA%\perutzms\ThermoScanHeaderExtractor\.thermo-license-accepted` |
| macOS | `~/Library/Application Support/ThermoScanHeaderExtractor/.thermo-license-accepted` |
| Linux | `$XDG_CONFIG_HOME/ThermoScanHeaderExtractor/.thermo-license-accepted` (default `~/.config/...`) |

To keep the acceptance file in another folder, set the environment variable `THERMO_SCAN_HEADER_EXTRACTOR_CONFIG_DIR` to the absolute path of that folder. `--agree-to-terms`, `--license-status` and extraction then use `.thermo-license-accepted` in that folder instead of the location above. The tool never falls back to the location above while the variable is set, even if the folder has no acceptance file. An empty value counts as not set. The value must be an absolute path to a folder, without spaces at the start or end; `~` is not expanded. Otherwise, the exit code is 5. This is useful for tests, containers and service accounts.

To only display the Thermo agreement without accepting it, run `ThermoScanHeaderExtractor --thermo-license`. This command never creates or changes `.thermo-license-accepted`.

To check whether the license has been accepted, run `ThermoScanHeaderExtractor --license-status`. The exit code is 0 if it has been accepted and 4 if not. Standard output is then one line of UTF-8 text: the path of the acceptance file. A short status message goes to standard error. If the location of the acceptance file cannot be determined, for example because `THERMO_SCAN_HEADER_EXTRACTOR_CONFIG_DIR` is invalid, the exit code is 5 and standard output is empty. The command never creates or changes the acceptance file or its folder.

To display only the current version, run `ThermoScanHeaderExtractor --version`. This command does not require license acceptance and prints a single version line, for example `0.3.0`.

The `help` subcommand, `--help`, and `-h` print command usage, the current `major.minor.patch` version, and the required Thermo attribution. By default, extraction writes the following sibling files beside the input (or in the directory given by `-o`), one for each level that has scans:

```text
<input-name>.ms1-trace.parquet
<input-name>.msn-trace.parquet
```

Use `--name <name>` to replace `<input-name>` in both output names. The supplied name is used literally, so `--name report.raw` creates `report.raw.ms1-trace.parquet` and `report.raw.msn-trace.parquet`. Names must be valid file names and cannot contain path separators; use `-o` to select the output directory.

Use `--ms1` or `--msn` to write only one level. With neither flag, or with both flags, the tool writes both levels. A file is only written for a level that has at least one scan. For example, an MS1-only acquisition gives no MSn file. If none of the requested levels has scans, nothing is written and the exit code is 8. If a requested level has no scans, an existing output file for that level, for example from an earlier run, is deleted, so it cannot be mistaken for a result of this input. Files of levels that were not requested are left alone. Each file is first written under a temporary name and only renamed when all files are complete, so other programs never see a half-written file. If the exit code is not 0, do not use the output files. Scans without an MS level are skipped, and a warning on standard error says how many there were. These are the Thermo scan types `Par`, `Nl` and `Ng` (parent, neutral loss and neutral gain scans), which Thermo describes as "ICIS Data Only". The two files have the required columns `scan_id`, `retention_time_sec`, and `ms_level`, plus the implemented optional columns `total_ion_current` and `base_peak_intensity`. `faims_cv` is included as nullable float32 only when the input contains non-zero FAIMS compensation voltages. `injection_time_ms` and all reserved columns are not currently emitted.

`scan_id` is the native Thermo scan number and each row represents exactly one spectrum. Retention time is expressed in seconds. Parquet footer metadata records schema version, generator, Thermo vendor, file level, precision mode, spectrum-level row granularity, source filename, source ID when valid instrument metadata is available, extraction timestamp, acquisition start time, and the vendor-recorded acquisition path. `source_filename` is the original RAW filename, including its extension, and is independent of any `--name` output override. `source_id` is composed as `<instrument-serial>_<creation-time>`, with the creation time formatted as `yyyyMMddHHmmss`, and is omitted when the instrument serial number is unavailable. `acquisition_start_time` is the local clock time of the acquisition PC when the run started, for example `2026-09-30T11:46:56`. It has no time zone suffix, because RAW files do not record the time zone, so do not read it as UTC. It is the same time as in `source_id` and is shared by both sibling files. `acquisition_path` is the vendor-recorded folder where the RAW file was acquired, rather than its current location. TSV output does not carry this metadata.

The default `low` precision mode stores `total_ion_current`, `base_peak_intensity`, and `retention_time_sec` as float32. `--high-precision` writes those three columns as float64. `faims_cv` is always float32. The `--tsv` flag writes the same selected MS1/MSn sibling files with a `.tsv` extension instead of `.parquet`. TSV files are UTF-8 text without a byte order mark, and every line ends with a line feed (LF), also on Windows. They have the same columns and units, but do not carry Parquet schema metadata.

The Parquet files follow schema version 1 of the [scan-trace specification](https://github.com/maxperutzlabs-ms/rawtrace-qc/blob/main/docs/scan-trace-spec.md) in the rawtrace-qc repository. The specification defines the file names, columns, units, row order, null values and footer metadata, and what programs that read the files can rely on. The `schema_version` key in the footer metadata records the schema version.

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

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Invalid usage (missing or empty input path, unknown option or argument, missing or empty value for `-o` or `--name`, invalid `--name`) |
| `2` | Input file not found or not accessible |
| `3` | Input file could not be read as a Thermo RAW file, either when opening it or while reading its scans |
| `4` | Thermo license terms have not been accepted; run `ThermoScanHeaderExtractor --agree-to-terms`. `--license-status` also uses this code |
| `5` | License handling failed: the configuration directory could not be determined, `THERMO_SCAN_HEADER_EXTRACTOR_CONFIG_DIR` is not a valid absolute folder path, the acceptance file could not be written, or the embedded agreement could not be loaded |
| `6` | Output files could not be written, for example because `-o` names an existing file, the folder cannot be created, access is denied, or the disk is full |
| `7` | Unexpected internal error. This is a bug; please report it together with the error message |
| `8` | None of the requested levels has scans, so no file was written |

Any other exit code means that the tool crashed, or that it comes from a newer version (see below). Error messages and warnings are written to standard error.

### Stable behaviour for programs that call the tool

From version 0.3.0, programs that call the tool can rely on the following. If any of it ever has to change, the changelog will list it as a breaking change.

- **Exit codes.** The codes in the table above keep their meaning. Later versions may add codes for new situations. New codes start at 9 and stay below 126. Treat any code you don't know as an error. After a crash, Linux and macOS report a signal (in Python, a negative return code), and Windows reports a large code such as 3221225477.
- **Read only exit codes and the output named here.** Standard error, and the standard output of extraction, `--agree-to-terms` and `--thermo-license`, are meant for people and may change. Only the standard output of `--license-status` and `--version` is meant for programs.
- **Commands stand alone.** `--agree-to-terms`, `--license-status`, `--thermo-license` and `--version` must be the only argument. Together with anything else, they give exit code 1.
- **`--license-status`** gives 0 if the license has been accepted and 4 if not. In both cases, standard output is exactly one line of UTF-8 text: the path of the acceptance file, followed by a line break (CR LF on Windows). If that path cannot be determined, it gives 5 and standard output is empty. It never creates or changes the acceptance file or its folder.
- **`--agree-to-terms`** can be run any number of times. If the license has already been accepted, it changes nothing and gives 0 again. It gives 5 if the acceptance cannot be recorded.
- **`THERMO_SCAN_HEADER_EXTRACTOR_CONFIG_DIR`** keeps its name and works as described in [CLI usage](#cli-usage).
- **Extraction.** The options `-o`, `--name`, `--ms1`, `--msn`, `--tsv` and `--high-precision` keep their names and meaning. So do the output file names (`<input-name>.ms1-trace.parquet` and `<input-name>.msn-trace.parquet`, or `.tsv`) and the rules above about which files are written, replaced and deleted.
- **Output content.** The Parquet files follow schema version 1 of the [scan-trace specification](https://github.com/maxperutzlabs-ms/rawtrace-qc/blob/main/docs/scan-trace-spec.md). Writing a new schema version is a breaking change. Changes to the specification that keep schema version 1 never change which files conform, so they don't affect the tool.
- **`--version`** prints one line that starts with the version number in the form `major.minor.patch`.

## Repository layout

```text
.
├── ThermoScanHeaderExtractor.sln        # Standalone solution
├── nuget.config                         # NuGet sources, incl. the local Thermo packages folder
├── LICENSE.txt                          # License file copied into the build output
├── THIRD-PARTY-NOTICES.txt              # Licenses of the bundled open-source libraries, copied into the build output
├── .github/workflows/                   # CI (build + sample extraction per runtime) and release builds
├── scripts/
│   ├── get-thermo-packages.ps1          # Downloads the Thermo NuGet packages
│   └── build.ps1                        # Cross-platform publish script (PowerShell 7)
├── src/ThermoScanHeaderExtractor/       # Console application
├── tests/data/thermo/                   # Small Thermo RAW fixture used by CI
├── third_party/Thermo.RawFileReader/
│   ├── thermo-package-source.json       # Pinned upstream repository, commit and package paths
│   ├── packages/                        # Downloaded .nupkg files (git-ignored)
│   ├── RawFileReader-DistributionLicense.md   # Thermo license text (converted from the .doc)
│   └── RawFileReader-DistributionLicense.doc  # Original Thermo license document
├── artifacts/publish/<runtime>/         # Build output (git-ignored, created by `build.ps1`)
└── README.md
```
