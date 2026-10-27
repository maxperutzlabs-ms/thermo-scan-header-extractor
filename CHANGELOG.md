# Changelog

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
