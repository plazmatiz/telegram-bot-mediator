using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Mediator.Services
{
    /// <summary>
    /// Background service managing the lifecycle and routing events into BotMediator.
    /// </summary>
    public class BotBackgroundService<TState> : BackgroundService where TState : struct, Enum
    {
        private readonly ITelegramBotClient _botClient;
        private readonly BotMediator<TState> _mediator;
        private readonly ILogger<BotBackgroundService<TState>> _logger;

        public BotBackgroundService(
            ITelegramBotClient botClient,
            BotMediator<TState> mediator,
            ILogger<BotBackgroundService<TState>> logger)
        {
            _botClient = botClient;
            _mediator = mediator;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var me = await _botClient.GetMe(stoppingToken);
                _logger.LogInformation("🤖 Bot @{Username} successfully started via Mediator workflow", me.Username);

                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
                };

                // Direct all incoming updates to our custom BotMediator
                _botClient.StartReceiving(
                    updateHandler: async (client, update, ct) =>
                    {
                        try
                        {
                            await _mediator.HandleUpdateAsync(client, update, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurred handling update with ID: {UpdateId}", update.Id);
                        }
                    },
                    errorHandler: (client, exception, ct) =>
                    {
                        _logger.LogError(exception, "Telegram API Error occurred in receiving loop");
                        return Task.CompletedTask;
                    },
                    receiverOptions: receiverOptions,
                    cancellationToken: stoppingToken
                );

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Bot background service received shutdown signal. Stopping...");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "An unhandled exception occurred in the Bot execution loop");
            }
        }
    }
}