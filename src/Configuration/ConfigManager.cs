using System.Globalization;

namespace ClunkyBorders.Configuration;

internal class ConfigManager
{
    public static Config Load(string configFilePath = "")
    {
        try
        {
            if (string.IsNullOrEmpty(configFilePath))
            {
                var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
                var exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                configFilePath = Path.Combine(exeDir, "config.toml");
            }

            if (!File.Exists(configFilePath))
            {
                Logger.Info("ConfigManager. Configuration file not found - loading default configuration.");
                return new Config();
            }

            Logger.Info($"ConfigManager. Log file loaded from: {configFilePath}");

            var toml = File.ReadAllLines(configFilePath);
            var config = ParseToml(toml);

            if(config.IsValid)
                return config;

            Logger.Error("ConfigManager. Configuration is invalid - loading default configuration.");
        }
        catch (Exception ex)
        {
            Logger.Error($"ConfigManager. Error processing the config file.", ex);
        }

        return new Config();
    }

    private static Config ParseToml(string[] lines)
    {
        lines = lines.Select(o => o.Trim())
            .Select(o => RemoveComments(o))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();

        var borderConfig = GetBorderConfig(lines);

        return new Config(borderConfig, new WindowConfig());
    }

    private static string RemoveComments(string line)
    {
        var i = line.IndexOf("#");

        if (i == 0)
            return string.Empty;
       
        if(i > 0)
            return line.Substring(0, i).Trim();

        return line;
    }

    private static BorderConfig GetBorderConfig(string[] lines)
    {
        bool isBorderSection = false;
        
        int width = 0;
        uint color = 0;
        int offset = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                var sectionName = line.Substring(1, line.Length - 2).Trim();
                isBorderSection = sectionName.Equals("border", StringComparison.OrdinalIgnoreCase);
            }

            if (isBorderSection)
            {
                var index = line.IndexOf("=");
                if (index > 0)
                {
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(++index).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "width":
                            if (int.TryParse(value, out int w))
                            {
                                width = w;
                            }
                            break;

                        case "color":
                            color = ParseHexValue(value);
                            break;

                        case "offset":
                            if (int.TryParse(value, out int g))
                            {
                                offset = g;
                            }
                            break;
                    }
                }
            }
        }

        return new BorderConfig
        {
            Width = width,
            Color = color,
            Offset = offset
        };
    }

    private static uint ParseHexValue(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(2);
        }

        if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint result))
        {
            return result;
        }

        return 0;
    }
}
