namespace ApiServer;

public class ConfigService
{
    private GoogleConfigs? _googleConfigs;
    
    public GoogleConfigs LoadGoogleConfigs(string path)
    {
        var jsonString = File.ReadAllText(path);
        _googleConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<GoogleConfigs>(jsonString);
        return _googleConfigs ?? new GoogleConfigs("", "");
    }

    public string GetGoogleClientId()
    {
        return _googleConfigs?.GoogleClientId ?? string.Empty;
    }
}