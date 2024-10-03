namespace AccountServer;

public class GoogleConfigs
{
    public GoogleConfigs(string googleClientId, string googleClientSecret)
    {
        GoogleClientId = googleClientId;
        GoogleClientSecret = googleClientSecret;
    }

    public string GoogleClientId { get; }
    public string GoogleClientSecret { get; }
}