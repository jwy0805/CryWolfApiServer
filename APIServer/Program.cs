using System.Security.Cryptography.X509Certificates;
using ApiServer.Services;
using ApiServer;
using ApiServer.DB;
using ApiServer.Providers;
using ApiServer.SignalRHub;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var certPath = builder.Configuration["Cert:Path"];
var certPwd = builder.Configuration["Cert:Password"];

// Add services to the container. -- StartUp.cs
var defaultConnectionString = 
    (builder.Environment.IsDevelopment() 
        ? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
        : builder.Configuration["DB:ConnectionString"])
        ?? throw new InvalidOperationException("DB Connection String is not configured.");
var jwtSecret =
    (builder.Environment.IsDevelopment()
        ? "CexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCexCex"
        : builder.Configuration["Jwt:Secret"]) ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.DictionaryKeyPolicy = null;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
    {
        policy
            .WithOrigins("https://localhost:7083")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("ProdCors", policy =>
    {
        policy
            .WithOrigins("https://hamonstudio.net")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSignalR();

builder.Services.AddScoped<TokenService>(provider => new TokenService(jwtSecret, 
    provider.GetRequiredService<AppDbContext>()));

builder.Services.AddScoped<TokenValidator>(provider => new TokenValidator(jwtSecret, 
        provider.GetRequiredService<AppDbContext>(),
        provider.GetRequiredService<TokenService>(),
        provider.GetRequiredService<ILogger<TokenValidator>>()));

builder.Services.AddHostedService<ExpiredTokenCleanupService>();
builder.Services.AddHostedService<UserManagementService>();
builder.Services.AddHostedService<DailyJob>();
builder.Services.AddHttpClient<ApiService>();
builder.Services.AddSingleton<ApiService>();
builder.Services.AddSingleton<MatchService>();
builder.Services.AddSingleton<TaskQueueService>();
builder.Services.AddScoped<WebSocketService>();
builder.Services.AddScoped<SinglePlayService>();
builder.Services.AddScoped<RewardService>();
builder.Services.AddScoped<ProductClaimService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<IDailyProductService, DailyProductService>();
builder.Services.AddScoped<IapService>();
builder.Services.AddTransient<UserService>();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
        options.UseMySql(defaultConnectionString,
            new MariaDbServerVersion(new Version(11, 3, 2)),
            mysqlOptions => mysqlOptions
                .EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

builder.Services.AddSingleton<CachedDataProvider>();

builder.Services.AddRazorPages();

builder.Logging.AddConsole();

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

// Check DB Connection
using (var scope = app.Services.CreateScope())
{   
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.OpenConnection();
        dbContext.Database.CloseConnection();
        Console.WriteLine($"DB Connection Success / commit 0211");
    }
    catch (Exception e)
    {
        throw new Exception($"DB Connection failed: {e.Message}");
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors(app.Environment.IsDevelopment() ? "DevCors" : "ProdCors");
app.MapRazorPages();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHub<SignalRHub>("/signalRHub");

app.Run();