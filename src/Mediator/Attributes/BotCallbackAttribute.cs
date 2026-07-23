namespace Telegram.Bot.Mediator.Mediator.Attributes
{
    /// <summary>
    /// Marks a method to handle callback queries. Supports exact match or prefix patterns.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BotCallbackAttribute : BotHandlerAttribute
    {
        /// <summary>
        /// Callback prefix or exact pattern (e.g., "edit_post:" or "cancel").
        /// </summary>
        public string CallbackPrefix { get; }

        public BotCallbackAttribute(string callbackPrefix, object? requiredState = null) : base(requiredState)
        {
            CallbackPrefix = callbackPrefix;
        }
    }
}