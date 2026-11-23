# Changelog

## [Unreleased]

### Changed
- A trace file is only written for a level that has at least one scan. Previously, when both levels were requested and the input had no MS1 scans, an MS1 file without rows was written.
- If none of the requested levels has scans, nothing is written and the exit code is 8. Previously, an input without MS1 or MSn scans gave an empty MS1 file and exit code 0, and a missing level requested with `--ms1` or `--msn` gave exit code 1.
- The Thermo RawFileReader packages are updated from 8.0.37 to 8.0.42. This also updates their dependency OpenMcdf from 2.3.0 to 3.1.4.
- The bundled Thermo RawFileReader license is updated to Thermo's new version from November 2026. It is now included as `RawFileReader-DistributionLicense.md` and `RawFileReader-DistributionLicense.doc`, which replace `RawFileReaderLicense.txt` and `RawFileReaderLicense.doc`. Users must now accept the license before use. The attribution and all license texts shown by the tool follow the new terms. To share the tool, send people the link to the release page instead of a copy.
- `acquisition_start_time` no longer ends in `Z`. It is the local clock time of the acquisition PC, for example `2026-09-30T11:46:56`, because RAW files do not record the time zone. Before, this clock time was written with a `Z`, which wrongly marked it as UTC. The tool no longer converts the time, so the result does not depend on the time zone of the computer that runs the extraction. Programs that read this value must accept it without `Z`.

### Fixed
- Output files are written under a temporary name and renamed when complete, so other programs never see a half-written file.
- If a requested level has no scans, an existing output file for that level is deleted. Before, a file from an earlier run stayed in place and looked like a result of the new input.
- Scans without an MS level (Thermo scan types `Par`, `Nl` and `Ng`) are still skipped, but a warning on standard error now says how many were skipped. Before, they were dropped without notice.
- TSV files are written as UTF-8 without a byte order mark (BOM). Before, some programs showed an invisible extra character at the start of the first column name (`scan_id`).

## [0.2.0] - 2026-10-29

### Added
- Linux (`linux-x64`, `linux-arm64`), macOS (`osx-x64`, `osx-arm64`), and Windows on Arm (`win-arm64`) support.
- Release archives for all six runtimes: `.zip` for Windows, `.tar.gz` for Linux and macOS.
- CI build and sample RAW file extraction on every supported runtime.

### Changed
- Thermo license acceptance is now recorded per user in the user's configuration directory instead of beside the executable. Existing users must run `--agree-to-terms` once again.
- `build.ps1` requires PowerShell 7, builds for the current OS and architecture by default, and refuses `osx-*` builds on non-macOS hosts so that macOS executables are code-signed.
- The license acceptance error message now shows the exact command and the expected marker file path.

## [0.1.0] - 2026-09-24

### Added
- Initial release of **ThermoScanHeaderExtractor**.
- Scan header metadata extraction from Thermo Scientific `.raw` files on Windows (`win-x64`).
- Default export to Snappy-compressed Apache Parquet table files.
- Optional combined tab-separated values (`--tsv`) export format.
- Scan filtering support via `--ms1` and `--msn` flags.
- High-precision numeric output mode via `--high-precision`.
- Custom output destination directory (`-o`) and output file name (`--name`).
- CLI helpers including `--version` and `help`.
- Embedded license viewing (`--thermo-license`) and acceptance record (`--agree-to-terms`).
- Single-file self-contained deployment configuration.
