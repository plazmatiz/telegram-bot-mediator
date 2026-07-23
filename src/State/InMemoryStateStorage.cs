using System.Collections.Concurrent;

namespace Telegram.Bot.Mediator.State
{
    /// <summary>
    /// Thread-safe in-memory storage for user states.
    /// </summary>
    public class InMemoryStateStorage<TState> : IUserStateStorage<TState>
    {
        private readonly ConcurrentDictionary<long, TState> _states = new();

        public Task<TState?> GetStateAsync(long userId, CancellationToken cancellationToken = default)
        {
            _states.TryGetValue(userId, out var state);
            return Task.FromResult(state);
        }

        public Task SetStateAsync(long userId, TState state, CancellationToken cancellationToken = default)
        {
            _states[userId] = state;
            return Task.CompletedTask;
        }

        public Task ClearStateAsync(long userId, CancellationToken cancellationToken = default)
        {
            _states.TryRemove(userId, out _);
            return Task.CompletedTask;
        }
    }
}