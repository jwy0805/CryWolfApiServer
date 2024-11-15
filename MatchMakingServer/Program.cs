using System.Security.Cryptography.X509Certificates;
using AccountServer.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var isLocal = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Local";

var certPath = Environment.GetEnvironmentVariable("CERT_PATH");
var certPwd = Environment.GetEnvironmentVariable("CERT_PASSWORD");

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});

builder.Services.AddHttpClient<ApiService>();
builder.Services.AddSingleton<ApiService>();

builder.Services.AddHostedServiceWithImplementation<JobService>();

builder.Services.AddHostedServiceWithImplementation<MatchMakingService>();

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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();