using System.Text.Json;

public static class ServerConfigLoader
{
    public static ServerConfig Load(string[] args)
    {
        string path = GetConfigPath(args);
        if (!File.Exists(path))
            throw new FileNotFoundException("Server configuration file was not found.", path);

        ServerConfig config = JsonSerializer.Deserialize<ServerConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) ?? throw new InvalidDataException("Server configuration is empty.");
        ServerConfigValidator.Validate(config);
        return config;
    }

    private static string GetConfigPath(string[] args)
    {
        if (args.Length == 0)
            return Path.Combine(AppContext.BaseDirectory, "serverconfig.json");
        if (args.Length == 2 && args[0] == "--config")
            return Path.GetFullPath(args[1]);
        throw new ArgumentException(
            "Usage: KarnivalServer [--config <path-to-serverconfig.json>]");
    }
}
