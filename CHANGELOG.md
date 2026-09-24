# Changelog

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
