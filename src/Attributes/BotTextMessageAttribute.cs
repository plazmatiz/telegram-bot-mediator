namespace Telegram.Bot.Mediator.Attributes
{
    /// <summary>
    /// Marks a method to handle any plain text input from a user, filtered by a required state.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BotTextMessageAttribute : BotHandlerAttribute
    {
        public BotTextMessageAttribute(object? requiredState = null) : base(requiredState)
        {
        }
    }
}