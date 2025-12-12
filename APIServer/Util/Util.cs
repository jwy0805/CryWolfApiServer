using System.Text.RegularExpressions;
using ApiServer.DB;

namespace ApiServer.Util;

public class Util
{
    private static readonly Random Random = new();
    
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
}