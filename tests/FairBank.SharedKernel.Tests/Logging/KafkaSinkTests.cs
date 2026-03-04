using System.Reflection;
using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using NSubstitute;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using FairBank.SharedKernel.Logging;

namespace FairBank.SharedKernel.Tests.Logging;

public class KafkaSinkTests : IDisposable
{
    private const string BootstrapServers = "localhost:9092";
    private const string Topic = "test-logs";

    private readonly IProducer<Null, string> _mockProducer;
    private readonly ConfluentKafkaSink _sut;

    public KafkaSinkTests()
    {
        // Create the sink with a dummy server (Confluent.Kafka producer creation is lazy)
        _sut = new ConfluentKafkaSink(BootstrapServers, Topic);

        // Replace the internal producer with a mock so we can capture messages
        _mockProducer = Substitute.For<IProducer<Null, string>>();
        var field = typeof(ConfluentKafkaSink).GetField(
            "_producer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(_sut, _mockProducer);
    }

    public void Dispose()
    {
        _mockProducer.Flush(Arg.Any<TimeSpan>());
        _sut.Dispose();
    }

    // ── Interface implementation ────────────────────────────

    [Fact]
    public void Sink_ImplementsILogEventSink()
    {
        _sut.Should().BeAssignableTo<ILogEventSink>();
    }

    [Fact]
    public void Sink_ImplementsIDisposable()
    {
        _sut.Should().BeAssignableTo<IDisposable>();
    }

    // ── Construction ────────────────────────────────────────

    [Fact]
    public void Constructor_WithValidParameters_DoesNotThrow()
    {
        var act = () => new ConfluentKafkaSink("localhost:9092", "my-topic");

        act.Should().NotThrow();
    }

    // ── Emit – JSON structure ───────────────────────────────

    [Fact]
    public void Emit_ProducesJsonWithRequiredFields()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Hello world");

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("Timestamp", out _).Should().BeTrue();
        root.TryGetProperty("Level", out _).Should().BeTrue();
        root.TryGetProperty("Message", out _).Should().BeTrue();
        root.TryGetProperty("Properties", out _).Should().BeTrue();
        root.TryGetProperty("Exception", out _).Should().BeTrue();
    }

    [Fact]
    public void Emit_SerializesLevelAsString()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Warning, "warn");

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Level").GetString().Should().Be("Warning");
    }

    [Fact]
    public void Emit_SerializesRenderedMessage()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Order processed");

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Message").GetString().Should().Be("Order processed");
    }

    [Fact]
    public void Emit_SerializesTimestampAsUtc()
    {
        var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var logEvent = CreateLogEvent(LogEventLevel.Information, "msg", timestamp: timestamp);

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);

        var ts = doc.RootElement.GetProperty("Timestamp").GetDateTime();
        ts.Kind.Should().Be(DateTimeKind.Utc);
        ts.Should().Be(new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    // ── Emit – structured properties (the fixed bug) ───────

    [Fact]
    public void Emit_PreservesStructuredProperties()
    {
        var properties = new List<LogEventProperty>
        {
            new("UserId", new ScalarValue("user-42")),
            new("OrderId", new ScalarValue(1001)),
        };
        var logEvent = CreateLogEvent(LogEventLevel.Information, "Order placed", properties: properties);

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("Properties");

        props.TryGetProperty("UserId", out var userId).Should().BeTrue();
        userId.GetString().Should().Be("user-42");

        props.TryGetProperty("OrderId", out var orderId).Should().BeTrue();
        orderId.GetString().Should().Be("1001");
    }

    [Fact]
    public void Emit_WithEmptyProperties_SerializesEmptyObject()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Debug, "simple");

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("Properties");

        props.ValueKind.Should().Be(JsonValueKind.Object);
        props.EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public void Emit_WithMultipleProperties_AllArePreserved()
    {
        var properties = new List<LogEventProperty>
        {
            new("Alpha", new ScalarValue("a")),
            new("Beta", new ScalarValue("b")),
            new("Gamma", new ScalarValue("c")),
        };
        var logEvent = CreateLogEvent(LogEventLevel.Information, "multi", properties: properties);

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("Properties");

        props.EnumerateObject().Count().Should().Be(3);
    }

    // ── Emit – exception handling ───────────────────────────

    [Fact]
    public void Emit_WithNullException_SerializesExceptionAsNull()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Information, "no error");

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("Exception").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Emit_WithException_SerializesExceptionString()
    {
        var exception = new InvalidOperationException("Something broke");
        var logEvent = CreateLogEvent(LogEventLevel.Error, "failure", exception: exception);

        _sut.Emit(logEvent);

        var json = CaptureProducedJson();
        var doc = JsonDocument.Parse(json);

        var exValue = doc.RootElement.GetProperty("Exception").GetString();
        exValue.Should().Contain("InvalidOperationException");
        exValue.Should().Contain("Something broke");
    }

    // ── Emit – produces to correct topic ────────────────────

    [Fact]
    public void Emit_ProducesToConfiguredTopic()
    {
        var logEvent = CreateLogEvent(LogEventLevel.Information, "msg");

        _sut.Emit(logEvent);

        _mockProducer.Received(1).Produce(
            Topic,
            Arg.Any<Message<Null, string>>(),
            Arg.Any<Action<DeliveryReport<Null, string>>>());
    }

    // ── Emit – does not throw on various inputs ─────────────

    [Theory]
    [InlineData(LogEventLevel.Verbose)]
    [InlineData(LogEventLevel.Debug)]
    [InlineData(LogEventLevel.Information)]
    [InlineData(LogEventLevel.Warning)]
    [InlineData(LogEventLevel.Error)]
    [InlineData(LogEventLevel.Fatal)]
    public void Emit_WithAnyLogLevel_DoesNotThrow(LogEventLevel level)
    {
        var logEvent = CreateLogEvent(level, "test message");

        var act = () => _sut.Emit(logEvent);

        act.Should().NotThrow();
    }

    // ── Dispose ─────────────────────────────────────────────

    [Fact]
    public void Dispose_FlushesProducer()
    {
        _sut.Dispose();

        _mockProducer.Received(1).Flush(Arg.Any<TimeSpan>());
    }

    [Fact]
    public void Dispose_DisposesProducer()
    {
        _sut.Dispose();

        _mockProducer.Received(1).Dispose();
    }

    // ── Helpers ─────────────────────────────────────────────

    private static LogEvent CreateLogEvent(
        LogEventLevel level,
        string message,
        Exception? exception = null,
        IEnumerable<LogEventProperty>? properties = null,
        DateTimeOffset? timestamp = null)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(message);

        return new LogEvent(
            timestamp ?? DateTimeOffset.UtcNow,
            level,
            exception,
            template,
            properties ?? []);
    }

    private string CaptureProducedJson()
    {
        var call = _mockProducer.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IProducer<Null, string>.Produce));

        var msg = (Message<Null, string>)call.GetArguments()[1]!;
        return msg.Value;
    }
}
