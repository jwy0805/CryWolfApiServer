namespace AccountServer.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostedServiceWithImplementation<TService>(
        this IServiceCollection services) 
        where TService : class, IHostedService 
    {
        services.AddSingleton<TService>();
        services.AddSingleton<IHostedService>(provider => 
            provider.GetService<TService>() ?? throw new InvalidOperationException());
        return services;
    }
}