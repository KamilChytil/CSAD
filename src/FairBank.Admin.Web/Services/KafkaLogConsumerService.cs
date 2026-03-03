using Confluent.Kafka;

namespace FairBank.Admin.Web.Services;

public class KafkaLogConsumerService : BackgroundService
{
    private readonly ILogger<KafkaLogConsumerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<string> _logs = new();
    private readonly int _maxLogs = 1000;

    public event Action? OnLogsUpdated;

    public KafkaLogConsumerService(
        ILogger<KafkaLogConsumerService> logger, 
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
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
                        var rawMessage = consumeResult.Message.Value;
                        await SaveLogToDb(rawMessage);

                        lock (_logs)
                        {
                            _logs.Insert(0, rawMessage); 
                            
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

    private async Task SaveLogToDb(string rawMessage)
    {
        try
        {
            // Simple parsing of "[Timestamp Level] Message" or similar
            // Expected format: [2026-03-03 13:12:18 Information] Message...
            var entry = new Data.LogEntry { Timestamp = DateTime.UtcNow, Message = rawMessage };

            if (rawMessage.StartsWith("["))
            {
                var endBracket = rawMessage.IndexOf(']');
                if (endBracket > 1)
                {
                    var meta = rawMessage.Substring(1, endBracket - 1).Split(' ');
                    if (meta.Length >= 3)
                    {
                        if (DateTime.TryParse($"{meta[0]} {meta[1]}", out var dt)) entry.Timestamp = dt;
                        entry.Level = meta[2];
                    }
                    entry.Message = rawMessage.Substring(endBracket + 1).Trim();
                }
            }

            // Heuristic for Service name
            if (entry.Message.Contains("ApiGateway")) entry.Service = "ApiGateway";
            else if (rawMessage.Contains("Identity")) entry.Service = "Identity";
            else if (rawMessage.Contains("Accounts")) entry.Service = "Accounts";

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.LogDbContext>();
            dbContext.Logs.Add(entry);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save log to SQLite");
        }
    }
}
