using System.Reflection;
using System.Text;

namespace ThermoScanHeaderExtractor;

internal static class LicenseAcceptance
{
    private const string MarkerFileName = ".thermo-license-accepted";
    private const string LicenseResourceName = "ThermoScanHeaderExtractor.ThirdParty.RawFileReaderLicense.txt";
    private const string AppAuthor = "perutzms";
    private const string AppName = "ThermoScanHeaderExtractor";

    internal static bool HasAcceptedTerms() => GetMarkerPath() is string markerPath && File.Exists(markerPath);

    internal static int AcceptTerms(string version)
    {
        string? markerPath = GetMarkerPath();
        if (markerPath == null)
        {
            Console.Error.WriteLine("ERROR: Could not determine the per-user configuration directory to record license acceptance.");
            return 5;
        }

        if (File.Exists(markerPath))
        {
            Console.WriteLine($"Thermo RawFileReader license terms have already been accepted for this user ({markerPath}).");
            return 0;
        }

        string licenseText;
        try
        {
            licenseText = GetLicenseText();
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"ERROR: Unable to load the embedded Thermo license: {exception.Message}");
            return 5;
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(new string('=', 38));
        Console.WriteLine("Thermo RawFileReader license agreement");
        Console.WriteLine(new string('=', 38));
        Console.WriteLine(licenseText);
        Console.WriteLine(new string('=', 38));
        Console.WriteLine("This agreement prohibits end users from redistributing this software to others.");
        Console.WriteLine("The `--agree-to-terms` command records your acceptance of these terms and enables extraction.");
        Console.WriteLine(new string('=', 38));
        Console.WriteLine();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(
                markerPath,
                $"thermo_rawfilereader_license_accepted_utc={DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
                $"thermoscanheaderextractor_version={version}{Environment.NewLine}");
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"ERROR: Could not record license acceptance: {exception.Message}");
            return 5;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"ERROR: Could not record license acceptance: {exception.Message}");
            return 5;
        }

        Console.WriteLine($"Terms accepted. Configuration saved to {markerPath}");
        return 0;
    }

    internal static int PrintThermoLicense()
    {
        string licenseText;
        try
        {
            licenseText = GetLicenseText();
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"ERROR: Unable to load the embedded Thermo license: {exception.Message}");
            return 5;
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(new string('=', 38));
        Console.WriteLine("Thermo RawFileReader license agreement");
        Console.WriteLine(new string('=', 38));
        Console.WriteLine(licenseText);
        Console.WriteLine(new string('=', 38));
        return 0;
    }

    internal static void PrintAcceptanceRequired()
    {
        Console.Error.WriteLine("ERROR: Thermo RawFileReader license terms have not been accepted for this user.");
        Console.Error.WriteLine("Before extracting data, run: ThermoScanHeaderExtractor --agree-to-terms");
        if (GetMarkerPath() is string markerPath)
            Console.Error.WriteLine($"Acceptance is recorded per user in: {markerPath}");
        Console.Error.WriteLine("The Thermo terms prohibit end users from redistributing this software to others.");
        Console.Error.WriteLine("The license is also distributed as RawFileReaderLicense.txt beside the executable.");
    }

    /// <summary>
    /// Returns the per-user marker path, following the Python platformdirs user_config_dir layout:
    /// Windows: %LOCALAPPDATA%\perutzms\ThermoScanHeaderExtractor
    /// macOS:   ~/Library/Application Support/ThermoScanHeaderExtractor
    /// Linux:   $XDG_CONFIG_HOME/ThermoScanHeaderExtractor (default ~/.config)
    /// Returns null when the base directory cannot be determined (for example, no home directory).
    /// </summary>
    private static string? GetMarkerPath()
    {
        // DoNotVerify: the default option returns an empty string when the base folder does not exist yet.
        string baseDirectory;
        string configDirectory;
        if (OperatingSystem.IsWindows())
        {
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            configDirectory = Path.Combine(baseDirectory, AppAuthor, AppName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Since .NET 8, LocalApplicationData maps to ~/Library/Application Support on macOS.
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            configDirectory = Path.Combine(baseDirectory, AppName);
        }
        else
        {
            // ApplicationData honours XDG_CONFIG_HOME and falls back to ~/.config.
            baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            configDirectory = Path.Combine(baseDirectory, AppName);
        }

        if (string.IsNullOrEmpty(baseDirectory) || !Path.IsPathRooted(baseDirectory))
            return null;

        return Path.Combine(configDirectory, MarkerFileName);
    }

    private static string GetLicenseText()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LicenseResourceName);
        if (stream == null)
            throw new InvalidOperationException("The RawFileReader license resource is missing.");

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}