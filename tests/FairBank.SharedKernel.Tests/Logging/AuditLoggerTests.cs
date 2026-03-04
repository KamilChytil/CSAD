using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FairBank.SharedKernel.Logging;

namespace FairBank.SharedKernel.Tests.Logging;

public class AuditLoggerTests
{
    private readonly ILogger<SerilogAuditLogger> _logger;
    private readonly SerilogAuditLogger _sut;

    public AuditLoggerTests()
    {
        _logger = Substitute.For<ILogger<SerilogAuditLogger>>();
        _sut = new SerilogAuditLogger(_logger);
    }

    [Fact]
    public void LogSecurityEvent_WithAllParameters_CallsLogInformation()
    {
        var userId = Guid.NewGuid();

        _sut.LogSecurityEvent("Login", "Success", userId, "User logged in", "192.168.1.1");

        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Information,
            default,
            default!,
            null,
            default!);
    }

    [Fact]
    public void LogSecurityEvent_WithRequiredParametersOnly_DoesNotThrow()
    {
        var act = () => _sut.LogSecurityEvent("Login", "Success");

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSecurityEvent_WithNullOptionalParameters_DoesNotThrow()
    {
        var act = () => _sut.LogSecurityEvent("Login", "Failure", null, null, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void LogSecurityEvent_WithNullUserId_CallsLogger()
    {
        _sut.LogSecurityEvent("PasswordReset", "Requested", null, "Reset email sent", "10.0.0.1");

        _logger.ReceivedWithAnyArgs(1).Log(
            LogLevel.Information,
            default,
            default!,
            null,
            default!);
    }

    [Fact]
    public void LogSecurityEvent_ImplementsIAuditLogger()
    {
        _sut.Should().BeAssignableTo<IAuditLogger>();
    }
}
