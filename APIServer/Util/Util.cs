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
}