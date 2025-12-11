using System.Security.Cryptography.X509Certificates;
using AccountServer.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);


var certPath = builder.Configuration["Cert:Path"];
var certPwd = builder.Configuration["Cert:Password"];

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

// -- StartUp.cs - Configure
if (!builder.Environment.IsDevelopment() && certPath != null && certPwd != null)
{   
    // Data Protection
#pragma warning disable CA1416
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .ProtectKeysWithCertificate(new X509Certificate2(certPath, certPwd));
#pragma warning restore CA1416
}

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();