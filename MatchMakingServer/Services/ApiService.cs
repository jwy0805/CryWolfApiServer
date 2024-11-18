using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace AccountServer.Services;

public class ApiService
{
    private readonly HttpClient _client;
    private readonly string _env = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Local";
    private const string ApiPortLocal= "5281";
    private const string SocketPort = "8081";
    
    private string BaseUrl => _env switch
    {
        "Local" => $"http://localhost:{ApiPortLocal}/api",
        "Dev" => $"http://crywolf-api/api",
        _ => throw new Exception("Invalid Environment")
    };

    private string BaseUrlSocket => _env switch
    {
        "Local" => $"http://localhost:{SocketPort}",
        "Dev" => $"http://crywolf-socket:{SocketPort}",
        _ => throw new Exception("Invalid Environment")
    };
    
    public ApiService(HttpClient client)
    {
        _client = client;
    }

    public async Task<T?> SendRequestToApiAsync<T>(string url, object? obj, HttpMethod method)
    {
        var sendUrl = $"{BaseUrl}/{url}";
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
    
    public async Task SendRequestToSocketAsync(string url, object? obj, HttpMethod method)
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
    }
}