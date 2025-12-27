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

            if(config.Validate())
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
        // todo: add parsing

        foreach (var line in lines)
        {
            // tirm whitespace
            // trim comments
            // check section
            // check for values
        }

        return new Config();
    }
}
