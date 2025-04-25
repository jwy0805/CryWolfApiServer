namespace ApiServer;

public class ConfigService
{
    private Configs? _configs;

    public ConfigService()
    {
        var path = Environment.GetEnvironmentVariable("CONFIG_PATH") ??
                   "/Users/jwy/Documents/Dev/CryWolf/Config/CryWolfAccountConfig.json";
        _configs = LoadConfigs(path);
    }
    
    private Configs LoadConfigs(string path)
    {
        var jsonString = File.ReadAllText(path);
        _configs = Newtonsoft.Json.JsonConvert.DeserializeObject<Configs>(jsonString);
        return _configs ?? new Configs("", "", "", "", "");
    }
    
    public string GetGoogleClientId()
    {
        return _configs?.GoogleClientId ?? string.Empty;
    }
    
    public string GetGoogleClientSecret()
    {
        return _configs?.GoogleClientSecret ?? string.Empty;
    }
    
    public string GetAppleTeamId()
    {
        return _configs?.AppleTeamId ?? string.Empty;
    }
    
    public string GetAppleBundleId()
    {
        return _configs?.AppleBundleId ?? string.Empty;
    }
    
    public string GetAdminPassword()
    {
        return _configs?.AdminPassword ?? string.Empty;
    }
}