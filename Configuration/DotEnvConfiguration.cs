// Smart Solar Microgrid Trading System - local .env configuration loader.
namespace SolarGridX.Api.Configuration;

/// <summary>
/// Loads development-only settings from a .env file before ASP.NET Core builds its configuration.
/// </summary>
public static class DotEnvConfiguration
{
    /// <summary>
    /// Adds unset environment variables from the project's .env file.
    /// </summary>
    public static void Load(string? directory = null)
    {
        var filePath = Path.Combine(directory ?? Directory.GetCurrentDirectory(), ".env");

        if (!File.Exists(filePath))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            if (key.Length > 0 && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
