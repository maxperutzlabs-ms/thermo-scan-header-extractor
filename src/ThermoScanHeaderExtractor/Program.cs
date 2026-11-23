/*
 * Program.cs
 * ==========
 * Entry point for ThermoScanHeaderExtractor.
 * Parses command-line arguments and delegates to ScanHeaderExtractor.Extract().
 *
 * Usage:
 *   ThermoScanHeaderExtractor input.raw
 *   ThermoScanHeaderExtractor input.raw -o output-directory --name output-name
 *   ThermoScanHeaderExtractor input.raw --high-precision
 *   ThermoScanHeaderExtractor input.raw --tsv
 *   ThermoScanHeaderExtractor input.raw --ms1
 *   ThermoScanHeaderExtractor input.raw --msn
 *   ThermoScanHeaderExtractor help
 *   ThermoScanHeaderExtractor --version
 *   ThermoScanHeaderExtractor --agree-to-terms
 *   ThermoScanHeaderExtractor --license-status
 *   ThermoScanHeaderExtractor --thermo-license
 */

using System;
using System.Reflection;
using System.Globalization;
using System.Text;

namespace ThermoScanHeaderExtractor
{
    internal static class Program
    {
        private const string ToolName = "ThermoScanHeaderExtractor";

        static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception exception)
            {
                // Exit code 7 is reserved for errors that no other code describes, i.e. bugs.
                Console.Error.WriteLine($"ERROR: Unexpected internal error. Please report this as a bug: {exception}");
                return 7;
            }
        }

        private static int Run(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 1 && IsHelpCommand(args[0]))
            {
                PrintHelp();
                return 0;
            }

            if (args.Length == 1 && args[0] == "--version")
            {
                Console.WriteLine(GetVersion());
                return 0;
            }

            if (args.Length == 1 && args[0] == "--agree-to-terms")
                return LicenseAcceptance.AcceptTerms(GetVersion());

            if (args.Length == 1 && args[0] == "--thermo-license")
                return LicenseAcceptance.PrintThermoLicense();

            if (args.Length == 1 && args[0] == "--license-status")
                return LicenseAcceptance.PrintAcceptanceStatus();

            // Arguments are checked before the license, so a usage error is always exit code 1.
            string? inputFile = null;
            string? outputDirectory = null;
            string? outputName = null;
            bool tsvOutput = false;
            bool highPrecision = false;
            bool ms1Only = false;
            bool msnOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--tsv")
                {
                    tsvOutput = true;
                }
                else if (args[i] == "--high-precision")
                {
                    highPrecision = true;
                }
                else if (args[i] == "--ms1")
                {
                    ms1Only = true;
                }
                else if (args[i] == "--msn")
                {
                    msnOnly = true;
                }
                else if (args[i] == "-o")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: -o requires an output path.");
                        return 1;
                    }

                    outputDirectory = args[++i];
                    if (outputDirectory.Length == 0)
                    {
                        Console.Error.WriteLine("ERROR: -o requires a non-empty output path.");
                        return 1;
                    }
                }
                else if (args[i] == "--name")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: --name requires an output name.");
                        return 1;
                    }

                    outputName = args[++i];
                    if (!IsOutputName(outputName))
                    {
                        Console.Error.WriteLine("ERROR: --name must be a non-empty valid file name without path separators.");
                        return 1;
                    }
                }
                else if (args[i].StartsWith("-", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"ERROR: Unknown option: {args[i]}");
                    PrintUsage(Console.Error);
                    return 1;
                }
                else if (args[i].Length == 0)
                {
                    Console.Error.WriteLine("ERROR: The input path is empty.");
                    return 1;
                }
                else if (inputFile == null)
                {
                    inputFile = args[i];
                }
                else
                {
                    Console.Error.WriteLine($"ERROR: Unexpected argument: {args[i]}");
                    PrintUsage(Console.Error);
                    return 1;
                }
            }

            if (inputFile == null)
            {
                PrintUsage(Console.Error);
                return 1;
            }

            int licenseStatus = LicenseAcceptance.GetAcceptanceStatus(out string? markerPath);
            if (licenseStatus == 4)
                LicenseAcceptance.PrintAcceptanceRequired(markerPath!);
            if (licenseStatus != 0)
                return licenseStatus;

            return ScanHeaderExtractor.ExtractAsync(
                    inputFile,
                    outputDirectory,
                    outputName,
                    tsvOutput,
                    highPrecision,
                    ms1Only,
                    msnOnly,
                    GetVersion())
                .GetAwaiter()
                .GetResult();
        }

        private static bool IsHelpCommand(string argument) =>
            argument is "help" or "--help" or "-h";

        private static bool IsOutputName(string outputName) =>
            !string.IsNullOrWhiteSpace(outputName) &&
            outputName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
            outputName.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0;

        private static string GetVersion() =>
            Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "unknown";

        private static void PrintUsage(TextWriter writer)
        {
            writer.WriteLine($"Usage: {ToolName} <input.raw> [--tsv] [--high-precision] [--ms1] [--msn]");
            writer.WriteLine("                              [-o output-directory] [--name output-name]");
        }

        private static void PrintHelp()
        {
            Console.WriteLine($"{ToolName} {GetVersion()}");
            Console.WriteLine("Extract scan-header metadata from a Thermo .raw file.");
            Console.WriteLine();
            PrintUsage(Console.Out);
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  help, --help, -h   Show this help and version.");
            Console.WriteLine("  --version          Show only the current version.");
            Console.WriteLine("  --agree-to-terms   Read and explicitly accept the Thermo license terms.");
            Console.WriteLine("  --thermo-license   Display the Thermo license without accepting it.");
            Console.WriteLine("  --license-status   Print the acceptance file path. Exit code 0: license accepted,");
            Console.WriteLine("                     4: not accepted, 5: the path cannot be determined.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -o <directory>     Write output files to this directory. Defaults to the input directory.");
            Console.WriteLine("  --name <name>      Replace the input filename in output file names.");
            Console.WriteLine("  --high-precision   Use float64 trace values; default is float32.");
            Console.WriteLine("  --tsv              Write TSV files instead of Parquet files.");
            Console.WriteLine("  --ms1              Write only the MS1 output file.");
            Console.WriteLine("  --msn              Write only the MSn output file.");
            Console.WriteLine();
            Console.WriteLine("Environment:");
            Console.WriteLine("  THERMO_SCAN_HEADER_EXTRACTOR_CONFIG_DIR");
            Console.WriteLine("                     Absolute folder path for the license acceptance file.");
            Console.WriteLine();
            Console.WriteLine("Thermo attribution:");
            Console.WriteLine("  RawFileReader. Copyright © 2016-2026 by Thermo Fisher Scientific, Inc. All rights reserved.");
        }
    }
}
