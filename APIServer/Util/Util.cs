using System.Text.RegularExpressions;
using ApiServer.DB;

namespace ApiServer.Util;

public class Util
{
    private static readonly Random Random = new();

    public static string? Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s[..max];
    }
    
    public static T[] ShuffleArray<T>(T[] array)
    {
        var shuffled = (T[])array.Clone();
        
        for (var i = 0; i < shuffled.Length; i++)
        {
            var j = Random.Next(i, shuffled.Length);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        
        return shuffled;
    }

    private static readonly Regex _engRx = new(@"^[A-Za-z0-9_]{3,20}$");
    private static readonly Regex _cjkrx = new(
        @"^[\uAC00-\uD7A3" +       // 한글 음절
        @"\u3040-\u309F" +       // 히라가나
        @"\u30A0-\u30FF" +       // 가타카나
        @"\u4E00-\u9FFF" +       // CJK Unified Ideographs(한자)
        @"0-9" + 
        @"]{3,10}$");
    
    public static bool IsValidUsername(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        
        return _engRx.IsMatch(name) || _cjkrx.IsMatch(name);
    }
    
    public static string ExtractUserTag(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return string.Empty;
        username = username.Trim();
        
        var hashIndex = username.LastIndexOf('#');
        if (hashIndex < 0 || hashIndex == username.Length - 1) return string.Empty;
        
        return username[(hashIndex + 1)..].Trim();
    }

    public static byte[] GzipUtf8(string s)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(s);
        using var ms = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(input, 0, input.Length);
        return ms.ToArray();
    }

    public static byte[] Sha256Utf8(string s)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
    }
}