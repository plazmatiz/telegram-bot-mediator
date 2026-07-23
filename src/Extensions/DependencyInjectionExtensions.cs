using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Mediator.Abstractions;
using Telegram.Bot.Mediator.State;

namespace Telegram.Bot.Mediator.Extensions
{
    public static class DependencyInjectionExtensions
    {
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
    }
}