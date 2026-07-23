namespace Telegram.Bot.Mediator.Mediator.Attributes;

/// <summary>
/// Base attribute for Telegram bot update handlers.
/// </summary>
public abstract class BotHandlerAttribute : Attribute
{
    /// <summary>
    /// The required user state for this handler to be triggered. If null, state is ignored.
    /// </summary>
    public object? RequiredState { get; }

    protected BotHandlerAttribute(object? requiredState)
    {
        RequiredState = requiredState;
    }
}