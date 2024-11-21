using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var isLocal = Environment.GetEnvironmentVariable("ENVIRONMENT") == "Local";
var certPath = Environment.GetEnvironmentVariable("CERT_PATH");
var certPwd = Environment.GetEnvironmentVariable("CERT_PASSWORD");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run("http://localhost:80");