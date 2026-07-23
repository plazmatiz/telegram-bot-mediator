using Telegram.Bot;
using Telegram.Bot.Types;
using Telegran.Bot.Mediator.Mediator.Abstractions;
using Telegran.Bot.Mediator.Mediator.Attributes;

namespace Telegran.Bot.Mediator.Tests;

public enum TestState
{
    Idle,
    WaitingForInput,
    Blocked
}

public class TestController : BotControllerBase
{
    public static bool CommandExecuted { get; set; }
    public static string? ReceivedParameter { get; set; }
    public static bool TextExecuted { get; set; }
    public static bool CallbackExecuted { get; set; }

    public static void Reset()
    {
        CommandExecuted = false;
        ReceivedParameter = null;
        TextExecuted = false;
        CallbackExecuted = false;
    }

    [BotCommand("/start", TestState.Idle)]
    public Task HandleStart(ITelegramBotClient client, string parameter)
    {
        CommandExecuted = true;
        ReceivedParameter = parameter;
        return Task.CompletedTask;
    }

    [BotTextMessage(TestState.WaitingForInput)]
    public Task HandleText(ITelegramBotClient client, Message message)
    {
        TextExecuted = true;
        return Task.CompletedTask;
    }

    [BotCallback("btn_", TestState.Idle)]
    public Task HandleCallback(ITelegramBotClient client, CallbackQuery callback)
    {
        CallbackExecuted = true;
        return Task.CompletedTask;
    }
}