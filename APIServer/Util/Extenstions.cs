namespace ApiServer.Util;

public class WeightedItem<T>
{
    public T Item { get; }
    public int Weight { get; }

    public WeightedItem(T item, int weight)
    {
        Item = item;
        Weight = weight;
    }
}

public static class RandomExtensions
{
    public static WeightedItem<T> PopRandomByWeight<T>(this List<WeightedItem<T>> pool, Random random)
    {
        int sum = pool.Sum(x => x.Weight);
        int pick = random.Next(1, sum + 1);
        int accumulatedW = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            accumulatedW += pool[i].Weight;
            if (pick <= accumulatedW)
            {
                var pickedItem = pool[i];
                pool.RemoveAt(i);
                return pickedItem;
            }
        }
        
        var lastItem = pool.Last();
        pool.RemoveAt(pool.Count - 1);
        return lastItem;
    }
}