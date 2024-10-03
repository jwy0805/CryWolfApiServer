using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace AccountServer.Services;

public class ApiService
{
    private readonly HttpClient _client;
    private const string MatchMakingPortLocal= "7003";
    private const string MatchMakingPortDev = "495";
    private const string SocketPortLocal = "8081";
    private const string SocketPortDev = "8001";
    private string BaseUrl => $"https://localhost:{MatchMakingPortLocal}/api";
    private string BaseUrlSocket => $"http://localhost:{SocketPortLocal}";

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