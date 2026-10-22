using System.Text;
using System.Text.Json;
using Community.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Community.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly string _exchangeName;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _exchangeName = _configuration["RabbitMQ:Exchange"] ?? "devradar.events";
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_initialized && _channel != null && _channel.IsOpen)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && _channel != null && _channel.IsOpen)
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
                UserName = _configuration["RabbitMQ:UserName"] ?? "devradar",
                Password = _configuration["RabbitMQ:Password"] ?? "devradar2026"
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            _initialized = true;
            _logger.LogInformation("Connected to RabbitMQ exchange '{Exchange}' at {Host}:{Port}", 
                _exchangeName, factory.HostName, factory.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Events will be skipped gracefully.");
            _initialized = false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishAsync<T>(string routingKey, T eventData, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureChannelAsync(cancellationToken);

            if (_channel == null || !_channel.IsOpen)
            {
                _logger.LogWarning("RabbitMQ channel is not open. Event for {RoutingKey} was skipped.", routingKey);
                return;
            }

            var envelope = new
            {
                eventId = Guid.NewGuid(),
                eventType = typeof(T).Name,
                timestamp = DateTime.UtcNow,
                data = eventData
            };

            var json = JsonSerializer.Serialize(envelope);
            var body = Encoding.UTF8.GetBytes(json);

            var basicProperties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: basicProperties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Published event {EventType} to exchange '{Exchange}' with key '{RoutingKey}'", 
                typeof(T).Name, _exchangeName, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to RabbitMQ for key {RoutingKey}", routingKey);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RabbitMQ connection.");
        }
        _lock.Dispose();
    }
}
