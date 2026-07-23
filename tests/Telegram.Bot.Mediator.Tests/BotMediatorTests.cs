using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Mediator.Mediator.State;
using Telegram.Bot.Types;
using Xunit;

namespace Telegram.Bot.Mediator.Tests;

public class BotMediatorTests
{
    private readonly Mock<ITelegramBotClient> _botClientMock;
    private readonly Mock<IUserStateStorage<TestState>> _stateStorageMock;
    private readonly Mock<ILogger<BotMediator<TestState>>> _loggerMock;
    private readonly ServiceProvider _serviceProvider;

    public BotMediatorTests()
    {
        TestController.Reset();

        _botClientMock = new Mock<ITelegramBotClient>();
        _stateStorageMock = new Mock<IUserStateStorage<TestState>>();
        _loggerMock = new Mock<ILogger<BotMediator<TestState>>>();

        var services = new ServiceCollection();
        services.AddScoped<TestController>();
        _serviceProvider = services.BuildServiceProvider();
    }

    private BotMediator<TestState> CreateMediator()
    {
        return new BotMediator<TestState>(
            _serviceProvider,
            _stateStorageMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task HandleUpdateAsync_ShouldRouteCommand_WhenStateMatches()
    {
        // Arrange
        var mediator = CreateMediator();
        var userId = 12345L;

        _stateStorageMock
            .Setup(s => s.GetStateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestState.Idle);

        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Text = "/start my_parameter",
                From = new User { Id = userId },
                Chat = new Chat { Id = 999 }
            }
        };

        // Act
        await mediator.HandleUpdateAsync(_botClientMock.Object, update, CancellationToken.None);

        // Assert
        Assert.True(TestController.CommandExecuted);
        Assert.Equal("my_parameter", TestController.ReceivedParameter);
    }

    [Fact]
    public async Task HandleUpdateAsync_ShouldNotRouteCommand_WhenStateDoesNotMatch()
    {
        // Arrange
        var mediator = CreateMediator();
        var userId = 12345L;

        // Встановлюємо стан, який не дозволяє виконати команду /start (очікується Idle)
        _stateStorageMock
            .Setup(s => s.GetStateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestState.Blocked);

        var update = new Update
        {
            Id = 2,
            Message = new Message
            {
                Text = "/start",
                From = new User { Id = userId },
                Chat = new Chat { Id = 999 }
            }
        };

        // Act
        await mediator.HandleUpdateAsync(_botClientMock.Object, update, CancellationToken.None);

        // Assert
        Assert.False(TestController.CommandExecuted);
    }

    [Fact]
    public async Task HandleUpdateAsync_ShouldRouteTextMessage_WhenInCorrectState()
    {
        // Arrange
        var mediator = CreateMediator();
        var userId = 12345L;

        _stateStorageMock
            .Setup(s => s.GetStateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestState.WaitingForInput);

        var update = new Update
        {
            Id = 3,
            Message = new Message
            {
                Text = "Some user text message",
                From = new User { Id = userId },
                Chat = new Chat { Id = 999 }
            }
        };

        // Act
        await mediator.HandleUpdateAsync(_botClientMock.Object, update, CancellationToken.None);

        // Assert
        Assert.True(TestController.TextExecuted);
    }
}