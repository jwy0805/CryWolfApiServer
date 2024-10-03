using AccountServer.DB;

namespace AccountServer;

public static class Extensions
{
    public static bool SaveChangesExtended(this AppDbContext dbContext)
    {
        try
        {
            dbContext.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            return false;
        }
    }
    
    public static T ToEnum<T>(this string enumString) where T : Enum
    {
        return (T)Enum.Parse(typeof(T), enumString);
    }
} 