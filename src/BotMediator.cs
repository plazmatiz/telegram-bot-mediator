using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Mediator.Abstractions;
using Telegram.Bot.Mediator.Attributes;
using Telegram.Bot.Mediator.State;

namespace Telegram.Bot.Mediator;

/// <summary>
/// Orchestrates incoming Telegram updates, coordinates user state evaluations,
/// and dynamically routes commands, text inputs, and callbacks to controllers.
/// Supports both single and multiple state constraints per handler.
/// </summary>
public class BotMediator<TState> where TState : struct, Enum
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserStateStorage<TState> _stateStorage;
    private readonly ILogger<BotMediator<TState>> _logger;
    private readonly List<HandlerMetadata> _handlers = new();

    public BotMediator(
        IServiceProvider serviceProvider,
        IUserStateStorage<TState> stateStorage,
        ILogger<BotMediator<TState>> logger)
    {
        _serviceProvider = serviceProvider;
        _stateStorage = stateStorage;
        _logger = logger;
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        var controllerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IBotController).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var type in controllerTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var commandAttr = method.GetCustomAttribute<BotCommandAttribute>();
                if (commandAttr != null)
                {
                    _handlers.Add(new HandlerMetadata(type, method, commandAttr));
                    continue;
                }

                var callbackAttr = method.GetCustomAttribute<BotCallbackAttribute>();
                if (callbackAttr != null)
                {
                    _handlers.Add(new HandlerMetadata(type, method, callbackAttr));
                    continue;
                }

                var textMessageAttr = method.GetCustomAttribute<BotTextMessageAttribute>();
                if (textMessageAttr != null)
                {
                    _handlers.Add(new HandlerMetadata(type, method, textMessageAttr));
                }
            }
        }
        _logger.LogInformation("BotMediator initialized. Registered {Count} handlers in total.", _handlers.Count);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            long? userId = GetUserIdFromUpdate(update);
            if (userId == null) return;

            TState? currentState = await _stateStorage.GetStateAsync(userId.Value, cancellationToken);
            _logger.LogInformation(
                "Processing Update ID: {UpdateId} | Type: {UpdateType} | User ID: {UserId} | Current State: {State}",
                update.Id, update.Type, userId, currentState?.ToString() ?? "null");

            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await ProcessMessageAsync(botClient, update, currentState, cancellationToken);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
            {
                await ProcessCallbackAsync(botClient, update, currentState, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing update {UpdateId}", update.Id);
        }
    }

    private async Task ProcessMessageAsync(ITelegramBotClient botClient, Update update, TState? currentState, CancellationToken cancellationToken)
    {
        var message = update.Message!;
        var text = message.Text!.Trim();

        // Extract the command token (the first word)
        var firstWord = text.Split(' ')[0];

        // Handle bot username in commands (e.g. /start@MyBot -> /start)
        var commandToken = firstWord.Split('@')[0];

        // 1. Try to match as a slash command first
        var matchedCommand = _handlers
            .Where(h => h.Attribute is BotCommandAttribute cmd &&
                        commandToken.Equals(cmd.Command, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Attribute.RequiredState != null)
            .FirstOrDefault(h => MatchState(h.Attribute.RequiredState, currentState));

        if (matchedCommand != null)
        {
            var parameter = text.Length > firstWord.Length
                ? text[firstWord.Length..].Trim()
                : string.Empty;

            _logger.LogInformation("Route matched command handler: {Controller}.{Method} with parameters: '{Params}'",
                matchedCommand.ControllerType.Name, matchedCommand.Method.Name, parameter);

            await InvokeHandlerAsync(matchedCommand, botClient, update, message, parameter, cancellationToken);
            return;
        }

        // 2. Automatically isolate commands from BotTextMessage.
        // If message starts with '/' but did not match any command above, we ignore it for generic text handlers.
        if (text.StartsWith('/'))
        {
            _logger.LogWarning("No command routing matched for incoming command: '{Text}' under state: '{State}'", text, currentState?.ToString() ?? "null");
            return;
        }

        // 3. Generic text handler (prioritises exact state handlers over null-state fallbacks)
        var matchedTextHandler = _handlers
            .Where(h => h.Attribute is BotTextMessageAttribute)
            .OrderByDescending(h => h.Attribute.RequiredState != null)
            .FirstOrDefault(h => MatchState(h.Attribute.RequiredState, currentState));

        if (matchedTextHandler != null)
        {
            _logger.LogInformation("Route matched text handler: {Controller}.{Method} for text: '{Text}'",
                matchedTextHandler.ControllerType.Name, matchedTextHandler.Method.Name, text);

            await InvokeHandlerAsync(matchedTextHandler, botClient, update, message, text, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No text routing matched for input: '{Text}' under state: '{State}'", text, currentState?.ToString() ?? "null");
        }
    }

    private async Task ProcessCallbackAsync(ITelegramBotClient botClient, Update update, TState? currentState, CancellationToken cancellationToken)
    {
        var callbackQuery = update.CallbackQuery!;
        var data = callbackQuery.Data!;

        var matchedHandler = _handlers
            .Where(h => h.Attribute is BotCallbackAttribute cb && data.StartsWith(cb.CallbackPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Attribute.RequiredState != null)
            .FirstOrDefault(h => MatchState(h.Attribute.RequiredState, currentState));

        if (matchedHandler != null)
        {
            var cbAttr = (BotCallbackAttribute)matchedHandler.Attribute;
            var parameter = data.Length > cbAttr.CallbackPrefix.Length
                ? data[cbAttr.CallbackPrefix.Length..]
                : string.Empty;

            _logger.LogInformation("Route matched callback handler: {Controller}.{Method} with parameters: '{Params}'",
                matchedHandler.ControllerType.Name, matchedHandler.Method.Name, parameter);

            await InvokeHandlerAsync(matchedHandler, botClient, update, callbackQuery, parameter, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No callback routing matched for payload: '{Data}' under state: '{State}'", data, currentState?.ToString() ?? "null");
        }
    }

    private async Task InvokeHandlerAsync(
        HandlerMetadata metadata,
        ITelegramBotClient botClient,
        Update update,
        object payload,
        string? parameter,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var instance = scope.ServiceProvider.GetRequiredService(metadata.ControllerType);

        if (instance is BotControllerBase baseController)
        {
            long? userId = GetUserIdFromUpdate(update);
            long? chatId = update.Type switch
            {
                UpdateType.Message => update.Message?.Chat.Id,
                UpdateType.CallbackQuery => update.CallbackQuery?.Message?.Chat.Id ?? update.CallbackQuery?.From.Id,
                _ => null
            };

            baseController.Context = new BotContext
            {
                ChatId = chatId ?? 0,
                UserId = userId ?? 0,
                OriginalUpdate = update
            };
        }

        var methodParams = metadata.Method.GetParameters();
        var args = new object?[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var paramType = methodParams[i].ParameterType;

            if (paramType == typeof(ITelegramBotClient))
                args[i] = botClient;
            else if (paramType == typeof(CancellationToken))
                args[i] = cancellationToken;
            else if (paramType == typeof(Message) && payload is Message msg)
                args[i] = msg;
            else if (paramType == typeof(CallbackQuery) && payload is CallbackQuery cb)
                args[i] = cb;
            else if (paramType == typeof(string) && parameter != null)
                args[i] = parameter;
            else
                args[i] = null;
        }

        try
        {
            var result = metadata.Method.Invoke(instance, args);
            if (result is Task task)
            {
                await task;
            }
            _logger.LogInformation("Successfully executed handler: {Controller}.{Method}", metadata.ControllerType.Name, metadata.Method.Name);
        }
        catch (Exception ex)
        {
            var real = ex is TargetInvocationException { InnerException: not null } tie
                ? tie.InnerException
                : ex;
            _logger.LogError(real, "Exception thrown inside handler execution: {Controller}.{Method}",
                metadata.ControllerType.Name, metadata.Method.Name);
            throw real;
        }
    }

    private bool MatchState(object? requiredState, TState? currentState)
    {
        // If the handler requires no state constraints, it matches anything
        if (requiredState == null) return true;

        var stateToCompare = currentState;

        // If the user currently has no state (null), safely treat it as the default "Idle" state if TState has one
        if (stateToCompare == null)
        {
            if (Enum.TryParse<TState>("Idle", out var idleState))
            {
                stateToCompare = idleState;
            }
        }

        if (stateToCompare == null) return false;

        // Case 1: Single TState enum value
        if (requiredState is TState reqStateEnum)
        {
            return EqualityComparer<TState>.Default.Equals(reqStateEnum, stateToCompare.Value);
        }

        // Case 2: Array or collection of TState values (e.g. UserState[] or object[])
        if (requiredState is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is TState stateEnum && EqualityComparer<TState>.Default.Equals(stateEnum, stateToCompare.Value))
                {
                    return true;
                }

                // Fallback comparison for boxed values
                if (item != null && item.ToString() == stateToCompare.Value.ToString())
                {
                    return true;
                }
            }
        }

        return false;
    }

    private long? GetUserIdFromUpdate(Update update)
    {
        return update.Type switch
        {
            UpdateType.Message => update.Message?.From?.Id,
            UpdateType.CallbackQuery => update.CallbackQuery?.From?.Id,
            _ => null
        };
    }

    private record HandlerMetadata(Type ControllerType, MethodInfo Method, BotHandlerAttribute Attribute);
}