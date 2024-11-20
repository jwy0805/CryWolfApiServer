using ApiServer.DB;

namespace ApiServer;

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
    
    public static async Task<bool> SaveChangesExtendedAsync(this AppDbContext dbContext)
    {
        try
        {
            await dbContext.SaveChangesAsync();
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