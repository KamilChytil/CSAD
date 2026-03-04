using Confluent.Kafka;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;

namespace FairBank.SharedKernel.Logging;

public class ConfluentKafkaSink : ILogEventSink, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public ConfluentKafkaSink(string bootstrapServers, string topic)
    {
        _topic = topic;
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public void Emit(LogEvent logEvent)
    {
        var payload = new
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Properties = logEvent.Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString().Trim('"')),
            Exception = logEvent.Exception?.ToString()
        };
        var json = JsonSerializer.Serialize(payload);

        _producer.Produce(_topic, new Message<Null, string> { Value = json });
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
