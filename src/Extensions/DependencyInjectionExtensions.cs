using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Mediator.Abstractions;
using Telegram.Bot.Mediator.Services;
using Telegram.Bot.Mediator.State;

namespace Telegram.Bot.Mediator.Extensions
{
    /// <summary>
    /// Extension methods for registering Telegram Bot Mediator services in the DI container.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Registers the core Telegram Bot Mediator services, in-memory state storage, and automatically discovers bot controllers.
        /// </summary>
        public static IServiceCollection AddTelegramMediator<TState>(this IServiceCollection services)
            where TState : struct, Enum
        {
            // Register state storage as singleton (for in-memory)
            services.AddSingleton<IUserStateStorage<TState>, InMemoryStateStorage<TState>>();

            // Register Mediator
            services.AddSingleton<BotMediator<TState>>();

            // Automatically register all IBotControllers from active assemblies
            var controllerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IBotController).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

            foreach (var type in controllerTypes)
            {
                services.AddScoped(type); // Scoped registration allows utilizing scoped DbContext in controllers
            }

            return services;
        }

        /// <summary>
        /// Registers the default background service managing the lifecycle and polling of the Telegram Bot.
        /// </summary>
        public static IServiceCollection AddTelegramBotHostedService<TState>(this IServiceCollection services)
            where TState : struct, Enum
        {
            services.AddHostedService<BotBackgroundService<TState>>();
            return services;
        }
    }
}