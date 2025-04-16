namespace ApiServer;

public class Configs
{
    public string GoogleClientId { get; }
    public string GoogleClientSecret { get; }
    public string AppleTeamId { get; }
    public string AppleBundleId { get; }

    public Configs(string googleClientId, string googleClientSecret, string appleTeamId, string appleBundleId)
    {
        GoogleClientId = googleClientId;
        GoogleClientSecret = googleClientSecret;
        AppleTeamId = appleTeamId;
        AppleBundleId = appleBundleId;
    }
}