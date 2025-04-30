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

    // public static List<T> WeightedSampler(IReadOnlyList<Tuple<int, int>> samples, int resultCount) where T : struct
    // {
    //     var itemIds = samples.Select(sample => sample.Item1).ToList();
    //     var weights = samples.Select(sample => sample.Item2).ToList();
    //     var totalWeight = samples.Sum(sample => sample.Item2);
    //     var cumulativeWeights = new int[samples.Count];
    //
    //     for (var i = 0; i < samples.Count; i++)
    //     {
    //         cumulativeWeights[i] += weights[i];
    //     }
    //
    //     var random = Random.Next(totalWeight);
    //     
    // }
}