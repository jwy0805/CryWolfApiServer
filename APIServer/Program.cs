using System.Security.Cryptography.X509Certificates;
using AccountServer;
using AccountServer.DB;
using AccountServer.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var isLocal = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Local";
var certPath = Environment.GetEnvironmentVariable("CERT_PATH");
var certPwd = Environment.GetEnvironmentVariable("CERT_PASSWORD");

builder.Services.AddSingleton<ConfigService>();

var configService = new ConfigService();
var path = Environment.GetEnvironmentVariable("CONFIG_PATH") ??
           "/Users/jwy/Documents/Dev/CryWolf/Config/CryWolfAccountConfig.json";
var appConfig = configService.LoadGoogleConfigs(path);

// Add services to the container. -- StartUp.cs
var defaultConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ??
                              builder.Configuration.GetConnectionString("DefaultConnection");
var jwtSecret = "QweksldfoqjksdlgSidjSDKgSkdnHGSEISKdndgkseG";

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = appConfig.GoogleClientId;
        options.ClientSecret = appConfig.GoogleClientSecret;
    });

builder.Services.AddScoped<TokenService>(provider => new TokenService(jwtSecret, 
    provider.GetRequiredService<AppDbContext>()));

builder.Services.AddScoped<TokenValidator>(provider => new TokenValidator(jwtSecret, 
        provider.GetRequiredService<AppDbContext>(),
        provider.GetRequiredService<TokenService>()));

builder.Services.AddHostedService<ExpiredTokenCleanupService>();

builder.Services.AddHttpClient<ApiService>();
builder.Services.AddSingleton<ApiService>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(defaultConnectionString, new MariaDbServerVersion(new Version(11, 3, 2)));
});

// -- StartUp.cs - Configure
if (isLocal == false)
{   
    // Data Protection
    if (certPath != null && certPwd != null)
    {
        #pragma warning disable CA1416
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
            .ProtectKeysWithCertificate(new X509Certificate2(certPath, certPwd));
        #pragma warning restore CA1416
    }
}

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Check DB Connection
using (var scope = app.Services.CreateScope())
{   
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.OpenConnection();
        dbContext.Database.CloseConnection();
        Console.WriteLine("DB Connection Success");
    }
    catch (Exception e)
    {
        throw new Exception($"DB Connection failed: {e.Message}");
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();