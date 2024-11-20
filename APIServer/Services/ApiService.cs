using System.Net.Http.Headers;
using System.Text;
using ApiServer.DB;
using Newtonsoft.Json;

namespace ApiServer.Services;

public class ApiService
{
    private readonly HttpClient _client;
    private const string MatchMakingPortLocal= "5083";
    private const string MatchMakingPortDev = "495";
    private const string SocketPort = "8081";
    
    private Env Environment => System.Environment.GetEnvironmentVariable("ENVIRONMENT") switch
    {
        "Local" => Env.Local,
        "Dev" => Env.Dev,
        "Stage" => Env.Stage,
        "Prod" => Env.Prod,
        _ => Env.Local
    };

    private string BaseUrlMatchMaking => Environment switch
    {
        Env.Local => $"http://localhost:{MatchMakingPortLocal}/match",
        Env.Dev => $"http://crywolf-matchmaking/match",
        _ => throw new Exception("Invalid Environment")
    };
    
    private string BaseUrlSocket => Environment switch
    {
        Env.Local => $"http://localhost:{SocketPort}",
        Env.Dev => $"http://crywolf-socket:{SocketPort}",
        _ => throw new Exception("Invalid Environment")
    };
    
    public ApiService(HttpClient client)
    {
        _client = client;
    }

    public async Task<T?> SendRequestAsync<T>(string url, object? obj, HttpMethod method)
    {
        var sendUrl = $"{BaseUrlMatchMaking}/{url}";
        byte[]? jsonBytes = null;
        if (obj != null)
        {
            var jsonStr = JsonConvert.SerializeObject(obj);
            jsonBytes = Encoding.UTF8.GetBytes(jsonStr);
        }
        
        var request = new HttpRequestMessage(method, sendUrl)
        {
            Content = new ByteArrayContent(jsonBytes ?? Array.Empty<byte>())
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        var response = await _client.SendAsync(request);

        if (response.IsSuccessStatusCode == false)
        {
            throw new Exception($"Error: {response.StatusCode} : {response.ReasonPhrase}");
        }
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(responseJson);
    }
    
    public async Task<T?> SendRequestToSocketAsync<T>(string url, object? obj, HttpMethod method)
    {
        var sendUrl = $"{BaseUrlSocket}/{url}";
        byte[]? jsonBytes = null;
        if (obj != null)
        {
            var jsonStr = JsonConvert.SerializeObject(obj);
            jsonBytes = Encoding.UTF8.GetBytes(jsonStr);
        }
        
        var request = new HttpRequestMessage(method, sendUrl)
        {
            Content = new ByteArrayContent(jsonBytes ?? Array.Empty<byte>())
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        var response = await _client.SendAsync(request);

        if (response.IsSuccessStatusCode == false)
        {
            throw new Exception($"Error: {response.StatusCode} : {response.ReasonPhrase}");
        }
        
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(responseJson);
    }
}