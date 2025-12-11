using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var certPath = builder.Configuration["Cert:Path"];
var certPwd = builder.Configuration["Cert:Password"];

// Add services to the container.
builder.Services.AddRazorPages();

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

app.UseAuthorization();

app.MapRazorPages();

app.Run();