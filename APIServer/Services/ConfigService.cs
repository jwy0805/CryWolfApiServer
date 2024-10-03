namespace AccountServer;

public class ConfigService
{
    public GoogleConfigs LoadGoogleConfigs(string path)
    {
        var jsonString = File.ReadAllText(path);
        var googleConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<GoogleConfigs>(jsonString);
        return googleConfigs ?? new GoogleConfigs("", "");
    }
}