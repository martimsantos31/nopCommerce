using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Events;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// Registers observability decorators AFTER the original services have been wired.
///
/// Order = 2001: runs right after NopStartup (2000) which registers all business
/// services including IEventPublisher. This startup replaces selected registrations
/// with instrumented decorators that add tracing spans without modifying original code.
/// </summary>
public class ObservabilityDecoratorsStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // --- Decorate IEventPublisher ---
        // Original: AddSingleton<IEventPublisher, EventPublisher> (NopStartup line 234)
        // EventPublisher is stateless (uses EngineContext.Current), so safe to wrap.
        DecorateEventPublisher(services);

        // --- Decorate IRepository<T> ---
        // Original: AddScoped(typeof(IRepository<>), typeof(EntityRepository<>)) (NopDbStartup)
        // Replace open generic with our instrumented version that internally creates
        // EntityRepository<T> with the same constructor dependencies.
        DecorateRepository(services);
    }

    private static void DecorateEventPublisher(IServiceCollection services)
    {
        // Remove the original singleton registration
        var original = services.FirstOrDefault(d => d.ServiceType == typeof(IEventPublisher));
        if (original != null)
            services.Remove(original);

        // Register the inner (concrete) EventPublisher so DI can resolve it
        services.TryAddSingleton<EventPublisher>();

        // Register the decorated version as the interface
        services.AddSingleton<IEventPublisher>(sp =>
            new InstrumentedEventPublisher(sp.GetRequiredService<EventPublisher>()));
    }

    private static void DecorateRepository(IServiceCollection services)
    {
        // Replace open generic: IRepository<T> → InstrumentedRepository<T>
        // InstrumentedRepository<T> takes the same ctor args as EntityRepository<T>
        // and creates the inner EntityRepository<T> internally.
        services.Replace(new ServiceDescriptor(
            typeof(IRepository<>),
            typeof(InstrumentedRepository<>),
            ServiceLifetime.Scoped));
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    public int Order => 2001;
}
