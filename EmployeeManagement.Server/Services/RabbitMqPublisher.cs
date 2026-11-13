using System.Text.Json;
using System.Threading.Channels;
using EmployeeManagement.Server.Configuration;
using EmployeeManagement.Server.Events;
using EmployeeManagement.Server.Interfaces;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EmployeeManagement.Server.Services;

public sealed class RabbitMqPublisher : BackgroundService, IRabbitMqPublisher
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly Channel<BusinessEvent> _pending;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _pending = Channel.CreateBounded<BusinessEvent>(new BoundedChannelOptions(_options.BufferCapacity)
        {
            SingleReader = true, FullMode = BoundedChannelFullMode.Wait
        });
    }

    public bool Publish(BusinessEvent businessEvent)
    {
        // Nuk presim rrjetin ne kerkesen HTTP. TryWrite refuzon kur buffer-i eshte plot.
        if (_options.Enabled && _options.HasCredentials && _pending.Writer.TryWrite(businessEvent))
            return true;

        _logger.LogWarning("Eventi {EventType} {EventId} NUK u pranua per publikim. RabbitMQ eshte i caktivizuar, mungojne kredencialet ose buffer-i eshte plot.",
            businessEvent.EventType, businessEvent.EventId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var businessEvent in _pending.Reader.ReadAllAsync(stoppingToken))
            {
                // Riprovimet ruajne te njejtin EventId; nje konfirmim i humbur mund te sjelle dublikata.
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                        await EnsureConnectedAsync(timeout.Token);
                        var properties = new BasicProperties
                        {
                            Persistent = true, ContentType = "application/json",
                            MessageId = businessEvent.EventId.ToString(), Type = businessEvent.EventType.ToString()
                        };
                        // mandatory + publisher confirms: suksesi raportohet vetem pas konfirmimit te broker-it.
                        await _channel!.BasicPublishAsync("", _options.QueueName, mandatory: true, properties,
                            JsonSerializer.SerializeToUtf8Bytes(businessEvent), timeout.Token);
                        _logger.LogInformation("RabbitMQ konfirmoi publikimin: {EventType} {EventId}, EntityId {EntityId}.",
                            businessEvent.EventType, businessEvent.EventId, businessEvent.EntityId);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "Publikimi NUK u konfirmua: {EventType} {EventId}, tentativa {Attempt}/3.",
                            businessEvent.EventType, businessEvent.EventId, attempt);
                        await DisconnectAsync();
                        if (attempt == 3)
                            _logger.LogError("Eventi {EventType} {EventId} u hoq nga buffer-i pas deshtimeve. Dorezimi NUK garantohet; ndryshimi ne DB mbetet i ruajtur.",
                                businessEvent.EventType, businessEvent.EventId);
                        else
                            await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Publisher po ndalet; eventet ende ne memorie mund te humbasin.");
        }
        finally { await DisconnectAsync(); }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true }) return;
        await DisconnectAsync();
        _connection = await _options.CreateFactory().CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        await _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
    }

    private async Task DisconnectAsync()
    {
        // Gabimet gjate pastrimit nuk duhet te ndalojne background service ose API-ne.
        try { if (_channel != null) await _channel.DisposeAsync(); }
        catch (Exception exception) { _logger.LogWarning(exception, "Nuk u mbyll channel-i i publisher-it."); }
        try { if (_connection != null) await _connection.DisposeAsync(); }
        catch (Exception exception) { _logger.LogWarning(exception, "Nuk u mbyll lidhja e publisher-it."); }
        _channel = null;
        _connection = null;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _pending.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }
}
