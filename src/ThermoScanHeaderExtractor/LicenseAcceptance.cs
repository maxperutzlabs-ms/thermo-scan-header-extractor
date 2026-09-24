using System.Reflection;
using System.Text;

namespace ThermoScanHeaderExtractor;

internal static class LicenseAcceptance
{
    private const string MarkerFileName = ".thermo-license-accepted";
    private const string LicenseResourceName = "ThermoScanHeaderExtractor.ThirdParty.RawFileReaderLicense.txt";

    internal static bool HasAcceptedTerms() => File.Exists(GetMarkerPath());

    internal static int AcceptTerms(string version)
    {
        string markerPath = GetMarkerPath();
        if (File.Exists(markerPath))
        {
            Console.WriteLine("Thermo RawFileReader license terms have already been accepted for this installation.");
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
        Console.Error.WriteLine("ERROR: Thermo RawFileReader license terms have not been accepted for this installation.");
        Console.Error.WriteLine("Before extracting data, run: ThermoScanHeaderExtractor.exe --agree-to-terms");
        Console.Error.WriteLine("The Thermo terms prohibit end users from redistributing this software to others.");
        Console.Error.WriteLine("The license is also distributed as RawFileReaderLicense.txt beside the executable.");
    }

    private static string GetMarkerPath()
    {
        string executableDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty)
            ?? AppContext.BaseDirectory;
        return Path.Combine(executableDirectory, MarkerFileName);
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