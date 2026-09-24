/*
 * ScanHeaderExtractor.cs
 * ================
 * Extracts per-scan statistics directly from a Thermo .raw file using the
 * ThermoFisher RawFileReader library.  Reads only scan-header metadata — no
 * peak arrays are decompressed — making it ~10–50× faster than converting
 * through mzParquet first.
 *
 * Output: specification-compliant MS1/MSn Parquet files by default, or TSV
 * sibling files when selected with the --tsv flag.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Parquet;
using Parquet.Schema;
using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.RawFileReader;

namespace ThermoScanHeaderExtractor
{
    static class ScanHeaderExtractor
    {
        private readonly record struct ScanRow(
            int ScanId,
            double TotalIonCurrent,
            double BasePeakIntensity,
            double RetentionTimeSec,
            int MsLevel,
            double? FaimsCv);

        /// <summary>
        /// Extract scan-level statistics from <paramref name="inputFile"/> and
        /// write them as Parquet or TSV using the scan-table specification's
        /// MS1/MSn sibling-file convention.
        /// </summary>
        /// <param name="inputFile">Path to the Thermo .raw file.</param>
        /// <param name="outputDirectory">
        ///   Optional output directory. The input file's directory is used
        ///   when this is null.
        /// </param>
        /// <param name="outputName">Optional replacement output basename.</param>
        /// <param name="tsvOutput">Whether to write TSV instead of Parquet.</param>
        /// <param name="highPrecision">Whether trace values use float64.</param>
        /// <param name="selectMs1">Whether MS1 was explicitly selected.</param>
        /// <param name="selectMsn">Whether MSn was explicitly selected.</param>
        /// <param name="version">Generator version for Parquet metadata.</param>
        /// <returns>Exit code: 0 = success, 2 = file not found, 3 = cannot open.</returns>
        public static async Task<int> ExtractAsync(
            string inputFile,
            string? outputDirectory,
            string? outputName,
            bool tsvOutput,
            bool highPrecision,
            bool selectMs1,
            bool selectMsn,
            string version)
        {
            if (!File.Exists(inputFile))
            {
                Console.Error.WriteLine($"ERROR: File not found: {inputFile}");
                return 2;
            }

            using IRawDataPlus raw = RawFileReaderAdapter.FileFactory(inputFile);
            if (!raw.IsOpen || raw.IsError)
            {
                Console.Error.WriteLine($"ERROR: Cannot open raw file: {raw.FileError}");
                return 3;
            }

            raw.SelectMsData();
            InstrumentData instrumentData = raw.GetInstrumentData();
            string? sourceId = instrumentData.IsValid &&
                               !string.IsNullOrWhiteSpace(instrumentData.SerialNumber)
                ? $"{instrumentData.SerialNumber}_{raw.CreationDate.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}"
                : null;

            // Locate the trailer-extra index for FAIMS CV (present only when FAIMS was used)
            int faimsIndex = -1;
            var trailerHeaders = raw.GetTrailerExtraHeaderInformation();
            for (int i = 0; i < trailerHeaders.Length; i++)
            {
                string label = trailerHeaders[i].Label;
                if (label.IndexOf("FAIMS CV", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("Compensation Voltage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    faimsIndex = i;
                    break;
                }
            }

            int firstScan = raw.RunHeader.FirstSpectrum;
            int lastScan  = raw.RunHeader.LastSpectrum;
            var rows = new List<ScanRow>(Math.Max(0, lastScan - firstScan + 1));
            bool hasNonZeroFaimsCv = false;

            for (int scanNum = firstScan; scanNum <= lastScan; scanNum++)
            {
                // ScanStatistics provides TIC and base peak directly from the scan header —
                // no peak decompression required.
                ScanStatistics stats = raw.GetScanStatsForScanNumber(scanNum);

                double tic      = stats.TIC;
                double basePeak = stats.BasePeakIntensity;
                double rt = stats.StartTime * 60d;

                IScanEvent scanEvent = raw.GetScanEventForScanNumber(scanNum);
                int msLevel = (int)scanEvent.MSOrder;

                // FAIMS CV from trailer extra; null means it was not acquired or parsed.
                double? faimsCv = null;
                if (faimsIndex >= 0)
                {
                    (bool[] valid, object[] values) =
                        raw.GetTrailerExtraDataForScanWithValidation(scanNum, trailerHeaders);
                    if (faimsIndex < valid.Length && valid[faimsIndex] &&
                        faimsIndex < values.Length &&
                        values[faimsIndex] != null &&
                        double.TryParse(
                            values[faimsIndex].ToString()?.Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double parsedCv))
                    {
                        faimsCv = parsedCv;
                        hasNonZeroFaimsCv |= parsedCv != 0d;
                    }
                }

                rows.Add(new ScanRow(scanNum, tic, basePeak, rt, msLevel, faimsCv));
            }

            bool includeFaimsCv = hasNonZeroFaimsCv;
            var ms1Rows = new List<ScanRow>();
            var msnRows = new List<ScanRow>();
            foreach (ScanRow row in rows)
            {
                if (row.MsLevel == 1)
                    ms1Rows.Add(row);
                else if (row.MsLevel > 1)
                    msnRows.Add(row);
            }

            bool selectAllLevels = selectMs1 == selectMsn;
            bool writeMs1 = selectAllLevels || selectMs1;
            bool writeMsn = selectAllLevels || selectMsn;

            if (!selectAllLevels && writeMs1 && ms1Rows.Count == 0)
            {
                Console.Error.WriteLine("ERROR: The input file contains no MS1 scans.");
                return 1;
            }

            if (!selectAllLevels && writeMsn && msnRows.Count == 0)
            {
                Console.Error.WriteLine("ERROR: The input file contains no MSn scans.");
                return 1;
            }

            if (tsvOutput)
            {
                WriteTsvFiles(
                    ms1Rows, msnRows, inputFile, outputDirectory, outputName,
                    includeFaimsCv, writeMs1, writeMsn);
            }
            else
            {
                await WriteParquetFilesAsync(
                    ms1Rows, msnRows, inputFile, outputDirectory, outputName,
                    includeFaimsCv, highPrecision, writeMs1, writeMsn, version,
                    raw.CreationDate.ToUniversalTime(),
                    raw.Path,
                    sourceId);
            }

            return 0;
        }

        private static void WriteTsvFiles(
            IReadOnlyList<ScanRow> ms1Rows,
            IReadOnlyList<ScanRow> msnRows,
            string inputFile,
            string? outputDirectory,
            string? outputName,
            bool includeFaimsCv,
            bool writeMs1,
            bool writeMsn)
        {
            string destinationDirectory = GetDestinationDirectory(inputFile, outputDirectory);
            Directory.CreateDirectory(destinationDirectory);
            string fileStem = outputName ?? Path.GetFileName(inputFile);

            if (writeMs1)
                WriteTsvFile(
                    ms1Rows,
                    Path.Combine(destinationDirectory, fileStem + ".ms1-trace.tsv"),
                    includeFaimsCv);
            if (writeMsn && msnRows.Count > 0)
                WriteTsvFile(
                    msnRows,
                    Path.Combine(destinationDirectory, fileStem + ".msn-trace.tsv"),
                    includeFaimsCv);
        }

        private static void WriteTsvFile(
            IReadOnlyList<ScanRow> rows,
            string outputFile,
            bool includeFaimsCv)
        {
            using var writer = new StreamWriter(outputFile, append: false, System.Text.Encoding.UTF8);
            writer.Write("scan_id\ttotal_ion_current\tbase_peak_intensity\tretention_time_sec\tms_level");
            if (includeFaimsCv)
                writer.Write("\tfaims_cv");
            writer.WriteLine();

            foreach (ScanRow row in rows)
            {
                writer.Write(
                    $"{row.ScanId}\t{row.TotalIonCurrent:F4}\t{row.BasePeakIntensity:F4}\t" +
                    $"{row.RetentionTimeSec:F6}\t{row.MsLevel}");
                if (includeFaimsCv)
                    writer.Write($"\t{row.FaimsCv?.ToString("G", CultureInfo.InvariantCulture) ?? ""}");
                writer.WriteLine();
            }
        }

        private static async Task WriteParquetFilesAsync(
            IReadOnlyList<ScanRow> ms1Rows,
            IReadOnlyList<ScanRow> msnRows,
            string inputFile,
            string? outputDirectory,
            string? outputName,
            bool includeFaimsCv,
            bool highPrecision,
            bool writeMs1,
            bool writeMsn,
            string version,
            DateTime acquisitionStartTime,
            string acquisitionPath,
            string? sourceId)
        {
            string destinationDirectory = GetDestinationDirectory(inputFile, outputDirectory);
            Directory.CreateDirectory(destinationDirectory);

            string fileStem = outputName ?? Path.GetFileName(inputFile);
            string sourceFilename = Path.GetFileName(inputFile);
            string extractedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            string acquisitionStartTimeText = acquisitionStartTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            if (writeMs1)
            {
                await WriteParquetFileAsync(
                    ms1Rows,
                    Path.Combine(destinationDirectory, fileStem + ".ms1-trace.parquet"),
                    "ms1",
                    includeFaimsCv,
                    highPrecision,
                    sourceFilename,
                    extractedAt,
                    acquisitionStartTimeText,
                    acquisitionPath,
                    sourceId,
                    version);
            }

            if (writeMsn && msnRows.Count > 0)
            {
                await WriteParquetFileAsync(
                    msnRows,
                    Path.Combine(destinationDirectory, fileStem + ".msn-trace.parquet"),
                    "msn",
                    includeFaimsCv,
                    highPrecision,
                    sourceFilename,
                    extractedAt,
                    acquisitionStartTimeText,
                    acquisitionPath,
                    sourceId,
                    version);
            }
        }

        private static string GetDestinationDirectory(string inputFile, string? outputDirectory) =>
            outputDirectory == null
                ? Path.GetDirectoryName(Path.GetFullPath(inputFile))!
                : Path.GetFullPath(outputDirectory);

        private static async Task WriteParquetFileAsync(
            IReadOnlyList<ScanRow> rows,
            string outputFile,
            string msLevelFile,
            bool includeFaimsCv,
            bool highPrecision,
            string sourceFilename,
            string extractedAt,
            string acquisitionStartTime,
            string acquisitionPath,
            string? sourceId,
            string version)
        {
            var scanIdField = new DataField<int>("scan_id");
            var msLevelField = new DataField<int>("ms_level");
            var fields = new List<Field> { scanIdField };

            if (highPrecision)
            {
                var totalIonCurrentField = new DataField<double>("total_ion_current");
                var basePeakField = new DataField<double>("base_peak_intensity");
                var retentionTimeField = new DataField<double>("retention_time_sec");
                fields.Add(totalIonCurrentField);
                fields.Add(basePeakField);
                fields.Add(retentionTimeField);
                fields.Add(msLevelField);

                if (includeFaimsCv)
                    fields.Add(new DataField<float?>("faims_cv"));

                await WriteParquetDataAsync(
                    rows, outputFile, new ParquetSchema(fields), scanIdField,
                    totalIonCurrentField, basePeakField, retentionTimeField, msLevelField,
                    includeFaimsCv ? (DataField<float?>)fields[^1] : null,
                    msLevelFile, "high", sourceFilename, extractedAt, acquisitionStartTime, acquisitionPath, sourceId, version);
            }
            else
            {
                var totalIonCurrentField = new DataField<float>("total_ion_current");
                var basePeakField = new DataField<float>("base_peak_intensity");
                var retentionTimeField = new DataField<float>("retention_time_sec");
                fields.Add(totalIonCurrentField);
                fields.Add(basePeakField);
                fields.Add(retentionTimeField);
                fields.Add(msLevelField);

                if (includeFaimsCv)
                    fields.Add(new DataField<float?>("faims_cv"));

                await WriteParquetDataAsync(
                    rows, outputFile, new ParquetSchema(fields), scanIdField,
                    totalIonCurrentField, basePeakField, retentionTimeField, msLevelField,
                    includeFaimsCv ? (DataField<float?>)fields[^1] : null,
                    msLevelFile, "low", sourceFilename, extractedAt, acquisitionStartTime, acquisitionPath, sourceId, version);
            }
        }

        private static async Task WriteParquetDataAsync<TFloat>(
            IReadOnlyList<ScanRow> rows,
            string outputFile,
            ParquetSchema schema,
            DataField<int> scanIdField,
            DataField<TFloat> totalIonCurrentField,
            DataField<TFloat> basePeakField,
            DataField<TFloat> retentionTimeField,
            DataField<int> msLevelField,
            DataField<float?>? faimsCvField,
            string msLevelFile,
            string precisionMode,
            string sourceFilename,
            string extractedAt,
            string acquisitionStartTime,
            string acquisitionPath,
            string? sourceId,
            string version)
            where TFloat : struct
        {
            int[] scanIds = new int[rows.Count];
            TFloat[] totalIonCurrents = new TFloat[rows.Count];
            TFloat[] basePeakIntensities = new TFloat[rows.Count];
            TFloat[] retentionTimes = new TFloat[rows.Count];
            int[] msLevels = new int[rows.Count];
            float?[]? faimsCvs = faimsCvField == null ? null : new float?[rows.Count];

            for (int i = 0; i < rows.Count; i++)
            {
                ScanRow row = rows[i];
                scanIds[i] = row.ScanId;
                totalIonCurrents[i] = ConvertTraceValue<TFloat>(row.TotalIonCurrent);
                basePeakIntensities[i] = ConvertTraceValue<TFloat>(row.BasePeakIntensity);
                retentionTimes[i] = ConvertTraceValue<TFloat>(row.RetentionTimeSec);
                msLevels[i] = row.MsLevel;
                if (faimsCvs != null)
                    faimsCvs[i] = row.FaimsCv.HasValue ? (float)row.FaimsCv.Value : null;
            }

            await using Stream outputStream = File.Create(outputFile);
            var options = new ParquetOptions { CompressionMethod = CompressionMethod.Snappy };
            await using ParquetWriter parquetWriter =
                await ParquetWriter.CreateAsync(schema, outputStream, options);
            var customMetadata = new Dictionary<string, string>
            {
                ["schema_version"] = "1",
                ["generator"] = $"ThermoScanHeaderExtractor/{version}",
                ["vendor"] = "thermo",
                ["ms_level_file"] = msLevelFile,
                ["precision_mode"] = precisionMode,
                ["row_granularity"] = "spectrum",
                ["source_filename"] = sourceFilename,
                ["extracted_at"] = extractedAt,
                ["acquisition_start_time"] = acquisitionStartTime,
                ["acquisition_path"] = acquisitionPath,
            };
            if (!string.IsNullOrWhiteSpace(sourceId))
                customMetadata["source_id"] = sourceId;
            parquetWriter.CustomMetadata = customMetadata;
            using ParquetRowGroupWriter rowGroupWriter = parquetWriter.CreateRowGroup();

            await rowGroupWriter.WriteAsync<int>(scanIdField, scanIds);
            await rowGroupWriter.WriteAsync<TFloat>(totalIonCurrentField, totalIonCurrents);
            await rowGroupWriter.WriteAsync<TFloat>(basePeakField, basePeakIntensities);
            await rowGroupWriter.WriteAsync<TFloat>(retentionTimeField, retentionTimes);
            await rowGroupWriter.WriteAsync<int>(msLevelField, msLevels);
            if (faimsCvField != null)
                await rowGroupWriter.WriteAsync<float>(faimsCvField, faimsCvs!);
        }

        private static TFloat ConvertTraceValue<TFloat>(double value)
            where TFloat : struct => typeof(TFloat) == typeof(float)
                ? (TFloat)(object)(float)value
                : (TFloat)(object)value;
    }
}
