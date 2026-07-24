# 🤖 Telegram.Bot.Mediator

A lightweight, state-based update routing framework for Telegram bots built on .NET. It simplifies bot architecture by organizing message handling logic into separate controllers and routing updates based on user states using a Finite State Machine (FSM).

[![NuGet Version](https://img.shields.io/nuget/v/Telegram.Bot.Mediator.svg)](https://www.nuget.org/packages/Telegram.Bot.Mediator)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

### 📦 What's Inside

#### Controllers
Independent, scoped classes inheriting from `BotControllerBase` that encapsulate message handling logic. Each controller has immediate, thread-safe access to the request context (such as `ChatId`, `UserId`, and the raw `Update` object).

#### State-Based Routing
Declarative routing using attributes (`BotCommand`, `BotTextMessage`, `BotCallback`) that direct incoming messages to specific controller actions based on the user's active state in the Finite State Machine.

#### Automatic Discovery
Seamless registration using assembly scanning. The framework automatically discovers all classes implementing `IBotController` and registers them with a `Scoped` lifetime, allowing safe injection of database contexts (like EF Core `DbContext`) or other scoped services.

#### Hosted Update Loop
A default background service utilizing long polling to quickly bootstrap development, allowing your bot to handle updates with minimal boilerplate configuration.

---

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
  - [1. Define User States](#1-define-user-states-enum)
  - [2. Create a Controller](#2-create-a-controller)
  - [3. Register Services in Program.cs](#3-register-services-in-programcs)
- [Advanced Concepts](#advanced-concepts)
  - [Accessing Request Context](#accessing-request-context)
  - [Custom State Storage](#custom-state-storage)
  - [Custom Update Loop (Manual Routing)](#custom-lifecycle-and-update-customization)
- [License](#license)

---

## Installation

Install the package via .NET CLI:

```bash
dotnet add package Telegram.Bot.Mediator
```

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
        // ChatId and UserId are properties inherited from BotControllerBase
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
await host.RunAsync();
```

---

## Advanced Concepts

### Accessing Request Context

When inheriting from `BotControllerBase`, the following context properties are automatically resolved for each incoming update:

* `ChatId` - The ID of the chat where the event occurred.
* `UserId` - The Telegram user ID who initiated the interaction.
* `Update` - The original raw `Update` object received from the Telegram API.

This avoids writing repetitive boilerplate code to manually parse updates inside controller actions.

### Custom State Storage

By default, `AddTelegramMediator<TState>` registers `InMemoryStateStorage<TState>`, which stores states in volatile memory. If your application restarts, active user states will be lost.

To persist states in a database (such as PostgreSQL, SQL Server, or Redis), implement the `IUserStateStorage<TState>` interface:

```csharp
using Telegram.Bot.Mediator.Mediator.State;

public class MyDatabaseStateStorage : IUserStateStorage<UserState>
{
    // Inject your DbContext, Redis multiplexer, or cache client here
    public MyDatabaseStateStorage() { }

    public async Task<UserState> GetStateAsync(long userId, CancellationToken ct = default)
    {
        // Fetch and return the state from your persistent store.
        // Return default state (e.g. UserState.Idle) if no record is found.
        return UserState.Idle; 
    }

    public async Task SetStateAsync(long userId, UserState state, CancellationToken ct = default)
    {
        // Save the state associated with the given userId
    }

    public async Task ClearStateAsync(long userId, CancellationToken ct = default)
    {
        // Remove or reset the state for the given userId
    }
}
```

Register your custom storage implementation as a singleton in `Program.cs`:

```csharp
builder.Services.AddSingleton<IUserStateStorage<UserState>, MyDatabaseStateStorage>();
```

### Custom Lifecycle and Update Customization

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

## License

This project is licensed under the [MIT License](LICENSE).