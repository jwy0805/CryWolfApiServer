namespace AccountServer.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostedServiceWithImplementation<TService>(
        this IServiceCollection services) 
        where TService : class, IHostedService 
    {
        services.AddSingleton<TService>();
        services.AddHostedService(provider => provider.GetRequiredService<TService>());
        return services;
    }
}