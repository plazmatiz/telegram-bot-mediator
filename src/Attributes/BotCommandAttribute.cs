namespace Telegram.Bot.Mediator.Attributes;

/// <summary>
/// Marks a method to handle text commands (e.g., /start).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class BotCommandAttribute : BotHandlerAttribute
{
    public string Command { get; }

    public BotCommandAttribute(string command, object? requiredState = null) : base(requiredState)
    {
        Command = command;
    }
}