namespace ApiServer;

public class Configs
{
    public string GoogleClientId { get; }
    public string GoogleClientSecret { get; }
    public string AppleTeamId { get; }
    public string AppleBundleId { get; }
    public string AdminPassword { get; }

    public Configs(string googleClientId, string googleClientSecret, string appleTeamId, string appleBundleId, string adminPassword)
    {
        GoogleClientId = googleClientId;
        GoogleClientSecret = googleClientSecret;
        AppleTeamId = appleTeamId;
        AppleBundleId = appleBundleId;
        AdminPassword = adminPassword;
    }
}