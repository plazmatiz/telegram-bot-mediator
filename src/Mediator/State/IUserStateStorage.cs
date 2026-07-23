namespace Telegran.Bot.Mediator.Mediator.State
{
    /// <summary>
    /// Defines contract for managing user states in the Telegram bot.
    /// </summary>
    /// <typeparam name="TState">The type representing user states (e.g., string or enum).</typeparam>
    public interface IUserStateStorage<TState>
    {
        Task<TState?> GetStateAsync(long userId, CancellationToken cancellationToken = default);
        Task SetStateAsync(long userId, TState state, CancellationToken cancellationToken = default);
        Task ClearStateAsync(long userId, CancellationToken cancellationToken = default);
    }
}