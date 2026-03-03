using Confluent.Kafka;
using FairBank.SharedKernel.Logging;
using Serilog;
using Serilog.Configuration;

namespace FairBank.SharedKernel;

public static class SerilogExtensions
{
    public static LoggerConfiguration Kafka(
        this LoggerSinkConfiguration loggerConfiguration,
        string bootstrapServers,
        string topic)
    {
        return loggerConfiguration.Sink(new ConfluentKafkaSink(bootstrapServers, topic));
    }
}
