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
        var message = logEvent.RenderMessage();
        
        var logOutput = $"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss} {logEvent.Level}] {message}";
        if (logEvent.Exception != null)
        {
            logOutput += $"\n{logEvent.Exception}";
        }

        _producer.Produce(_topic, new Message<Null, string> { Value = logOutput });
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
