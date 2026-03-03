using Confluent.Kafka;

namespace FairBank.Admin.Web.Services;

public class KafkaLogConsumerService : BackgroundService
{
    private readonly ILogger<KafkaLogConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<string> _logs = new();
    private readonly int _maxLogs = 1000;

    public event Action? OnLogsUpdated;

    public KafkaLogConsumerService(ILogger<KafkaLogConsumerService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public IReadOnlyList<string> GetLatestLogs()
    {
        lock (_logs)
        {
            return _logs.ToList();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "kafka:9092",
            GroupId = "fairbank-admin-web-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        try
        {
            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            var topic = _configuration["Kafka:Topic"] ?? "fairbank-logs";
            consumer.Subscribe(topic);

            _logger.LogInformation("Kafka Consumer started listening to topic: {Topic}", topic);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult != null && !string.IsNullOrEmpty(consumeResult.Message.Value))
                    {
                        var logMessage = consumeResult.Message.Value;

                        lock (_logs)
                        {
                            _logs.Insert(0, logMessage); 
                            
                            if (_logs.Count > _maxLogs)
                            {
                                _logs.RemoveAt(_logs.Count - 1);
                            }
                        }

                        OnLogsUpdated?.Invoke();
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError(e, "Error consuming message from Kafka.");
                }
            }

            consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error running Kafka Consumer.");
        }
    }
}
