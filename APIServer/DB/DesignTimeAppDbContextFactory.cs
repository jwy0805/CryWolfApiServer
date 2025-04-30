using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApiServer.DB;

public class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string is not set.");
        }
        
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        return new AppDbContext(builder.Options);
    }
}