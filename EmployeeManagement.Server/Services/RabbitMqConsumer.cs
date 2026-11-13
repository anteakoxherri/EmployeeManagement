using System.Text.Json;
using EmployeeManagement.Server.Configuration;
using EmployeeManagement.Server.Events;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EmployeeManagement.Server.Services;

public sealed class RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.ConsumerEnabled || !settings.HasCredentials)
        {
            logger.LogWarning("RabbitMQ Consumer nuk u nis: kontrolloni Enabled, ConsumerEnabled dhe kredencialet.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Consumer ka lidhjen/channel-in e vet; nuk ndan channel me publisher-in.
                await using var connection = await settings.CreateFactory().CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await channel.QueueDeclareAsync(settings.QueueName, durable: true, exclusive: false, autoDelete: false,
                    cancellationToken: stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    BusinessEvent businessEvent;
                    try
                    {
                        // Body perdoret brenda callback-ut, para se biblioteka te liroje memorien e mesazhit.
                        businessEvent = JsonSerializer.Deserialize<BusinessEvent>(delivery.Body.Span)
                            ?? throw new JsonException("Missing event.");
                        if (businessEvent.EventId == Guid.Empty || string.IsNullOrWhiteSpace(businessEvent.EntityId) ||
                            businessEvent.Timestamp == default || !Enum.IsDefined(businessEvent.EventType))
                            throw new JsonException("Invalid event.");
                    }
                    catch (JsonException)
                    {
                        // Mesazhi i pavlefshem nuk rifutet pafund ne radhe dhe permbajtja nuk shkruhet ne log.
                        logger.LogWarning("Mesazh RabbitMQ i pavlefshem u refuzua pa requeue.");
                        await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken: stoppingToken);
                        return;
                    }

                    logger.LogInformation("Event i konsumuar: {EventType}, EventId {EventId}, Timestamp {Timestamp}, EntityId {EntityId}, ProjectId {ProjectId}, TaskId {TaskId}, UserId {UserId}, AssignedToUserId {AssignedToUserId}.",
                        businessEvent.EventType, businessEvent.EventId, businessEvent.Timestamp, businessEvent.EntityId,
                        businessEvent.ProjectId, businessEvent.TaskId, businessEvent.UserId, businessEvent.AssignedToUserId);
                    // ACK vetem pas perpunimit. Rilidhja mund te sjelle ridorezim: nuk premtojme exactly-once.
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                };
                await channel.BasicConsumeAsync(settings.QueueName, autoAck: false, consumer, stoppingToken);
                logger.LogInformation("RabbitMQ Consumer po degjon radhen {QueueName}.", settings.QueueName);
                while (connection.IsOpen && channel.IsOpen)
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "RabbitMQ Consumer humbi lidhjen ose nuk u lidh. Do te riprovoje; API vazhdon te punoje.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
