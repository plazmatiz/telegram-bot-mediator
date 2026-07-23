# Telegram.Bot.Mediator

A lightweight mediator library for routing incoming updates in Telegram bots on the .NET platform. It simplifies bot architecture by organizing message handling logic into separate controllers and routing updates based on user states (FSM — Finite State Machine).

## Key Features

* **Automatic Controller Discovery**: Automatically scans assemblies, registers, and configures all classes implementing `IBotController`.
* **State-Based Routing**: Route commands, plain text, and callback queries to specific handler methods based on the user's current state.
* **Scoped Dependency Support**: Controllers are registered with a `Scoped` lifetime, allowing safe injection of scoped services (such as Entity Framework's `DbContext`).
* **Request Context**: Immediate access to Chat ID, User ID, and the original `Update` object via the base controller class.
* **Built-in Lifecycle Management**: Includes a default background service using long polling to handle incoming updates.

---

## Installation

Install the package via .NET CLI:

```bash
dotnet add package MyBot.Mediator
```
*(Replace `MyBot.Mediator` with your actual package ID after publishing)*

---

## Quick Start

### 1. Define User States (Enum)

Create an enum that represents the conversational steps or states of your user:

```csharp
public enum UserState
{
    Idle,
    EnteringName,
    EnteringAge
}
```

### 2. Create a Controller

All controllers must inherit from `BotControllerBase`. Use attributes to define the routing conditions for each method.

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Mediator.Mediator.Abstractions;
using Telegram.Bot.Mediator.Mediator.Attributes;
using Telegram.Bot.Mediator.Mediator.State;

public class RegistrationController : BotControllerBase
{
    private readonly IUserStateStorage<UserState> _stateStorage;

    public RegistrationController(IUserStateStorage<UserState> stateStorage)
    {
        _stateStorage = stateStorage;
    }

    // Handles the /start command only if the user is in the Idle state
    [BotCommand("/start", UserState.Idle)]
    public async Task StartCommand(ITelegramBotClient bot, CancellationToken ct)
    {
        await bot.SendTextMessageAsync(ChatId, "Hello! Please enter your name:", cancellationToken: ct);
        
        // Transition user to the next state
        await _stateStorage.SetStateAsync(UserId, UserState.EnteringName, ct);
    }

    // Handles any text input while the user is in the EnteringName state
    [BotTextMessage(UserState.EnteringName)]
    public async Task HandleName(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        string name = message.Text ?? string.Empty;
        await bot.SendTextMessageAsync(ChatId, $"Thank you, {name}! Now, please enter your age:", cancellationToken: ct);
        
        await _stateStorage.SetStateAsync(UserId, UserState.EnteringAge, ct);
    }

    // Handles callback queries starting with "confirm_" while in the EnteringAge state
    [BotCallback("confirm_", UserState.EnteringAge)]
    public async Task HandleConfirmation(ITelegramBotClient bot, CallbackQuery callback, string parameter, CancellationToken ct)
    {
        // 'parameter' contains the callback data trailing after the prefix "confirm_"
        await bot.SendTextMessageAsync(ChatId, $"Registration complete! Parameter received: {parameter}", cancellationToken: ct);
        await _stateStorage.ClearStateAsync(UserId, ct);
    }
}
```

### 3. Register Services in `Program.cs`

Configure the DI container using the provided extension methods. This registers the mediator, controller instances, and the polling background service.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Mediator.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// 1. Register the standard TelegramBotClient
builder.Services.AddSingleton<ITelegramBotClient>(provider => 
    new TelegramBotClient("YOUR_TELEGRAM_BOT_TOKEN"));

// 2. Register mediator, in-memory state storage, and auto-discover controllers
builder.Services.AddTelegramMediator<UserState>();

// 3. Register the default polling service (handles Message and CallbackQuery updates by default)
builder.Services.AddTelegramBotHostedService<UserState>();

var host = builder.Build();
host.Run();
```

---

## Custom Lifecycle and Update Customization

The default background service registered via `AddTelegramBotHostedService<TState>` polls for updates with standard configurations (restricted to `UpdateType.Message` and `UpdateType.CallbackQuery`).

If you need to support other update types, utilize Webhooks, or manage the receiver loop differently, you can omit the default background service and build your own.

To route updates manually, inject `BotMediator<TState>` into your custom receiver or controller, and pass incoming updates:

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Mediator;

public class CustomUpdateReceiver
{
    private readonly BotMediator<UserState> _mediator;

    public CustomUpdateReceiver(BotMediator<UserState> mediator)
    {
        _mediator = mediator;
    }

    public async Task ProcessUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        // Pass the update to the mediator for routing and state-matching execution
        await _mediator.HandleUpdateAsync(botClient, update, ct);
    }
}
```

---

## User State Management

By default, `AddTelegramMediator<TState>` registers `InMemoryStateStorage<TState>`, which stores states in volatile memory. If your application restarts, all active user states will be lost.

To persist states in a database (such as PostgreSQL, SQL Server, or Redis), implement the `IUserStateStorage<TState>` interface and register your implementation as a singleton before or after calling `AddTelegramMediator`:

```csharp
builder.Services.AddSingleton<IUserStateStorage<UserState>, MyDatabaseStateStorage>();
```

## License

This project is licensed under the [MIT License](LICENSE).