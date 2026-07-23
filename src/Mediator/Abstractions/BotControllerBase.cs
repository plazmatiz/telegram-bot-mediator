using Telegram.Bot.Types;

namespace Telegram.Bot.Mediator.Mediator.Abstractions;

/// <summary>
/// Represents the contextual data of the current Telegram update.
/// </summary>
public class BotContext
{
    public long ChatId { get; init; }
    public long UserId { get; init; }
    public Update OriginalUpdate { get; init; } = null!;
}

/// <summary>
/// Base class for all bot controllers. Provides easy access to chat, user and update context.
/// </summary>
public abstract class BotControllerBase : IBotController
{
    /// <summary>
    /// Gets the context of the current update. Filled automatically by the Mediator.
    /// </summary>
    public BotContext Context { get; internal set; } = null!;

    /// <summary>
    /// Helper property to quickly get the Chat ID.
    /// </summary>
    protected long ChatId => Context.ChatId;

    /// <summary>
    /// Helper property to quickly get the User ID.
    /// </summary>
    protected long UserId => Context.UserId;
}