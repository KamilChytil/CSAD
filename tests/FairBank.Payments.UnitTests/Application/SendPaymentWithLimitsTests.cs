using FluentAssertions;
using NSubstitute;
using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Application.Payments.Commands.SendPayment;
using FairBank.Payments.Application.Ports;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;

namespace FairBank.Payments.UnitTests.Application;

public class SendPaymentWithLimitsTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IAccountsServiceClient _accountsClient = Substitute.For<IAccountsServiceClient>();
    private readonly INotificationClient _notificationClient = Substitute.For<INotificationClient>();
    private readonly IIdentityClient _identityClient = Substitute.For<IIdentityClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Guid _senderAccountId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _parentId = Guid.NewGuid();

    private AccountInfo CreateSenderAccount(decimal balance = 10_000m) =>
        new(_senderAccountId, _ownerId, "FAIR-000001", balance, "CZK", true);

    private SendPaymentCommand CreateCommand(decimal amount = 5_000m) =>
        new(_senderAccountId, "FAIR-000002", amount, "CZK", "Test payment");

    private SendPaymentCommandHandler CreateHandler() =>
        new(_paymentRepository, _accountsClient, _notificationClient, _identityClient, _unitOfWork);

    [Fact]
    public async Task Handle_WhenAmountExceedsApprovalThreshold_ShouldCreatePendingTransaction()
    {
        // Arrange
        var senderAccount = CreateSenderAccount();
        _accountsClient.GetAccountByIdAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(senderAccount);

        _accountsClient.GetSpendingLimitAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(RequiresApproval: true, ApprovalThreshold: 1_000m, Currency: "CZK"));

        var pendingId = Guid.NewGuid();
        _accountsClient.CreatePendingTransactionAsync(
                _senderAccountId, Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), _ownerId, Arg.Any<CancellationToken>())
            .Returns(new PendingTransactionInfo(pendingId, "Pending"));

        _identityClient.GetUserAsync(_ownerId, Arg.Any<CancellationToken>())
            .Returns(new UserInfo(_ownerId, "Jan", "Novak", "Child", _parentId));

        var handler = CreateHandler();
        var command = CreateCommand(amount: 5_000m); // exceeds 1_000 threshold

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("PendingApproval");

        await _accountsClient.Received(1)
            .GetSpendingLimitAsync(_senderAccountId, Arg.Any<CancellationToken>());

        await _accountsClient.Received(1)
            .CreatePendingTransactionAsync(
                _senderAccountId, 5_000m, "CZK",
                Arg.Any<string>(), _ownerId, Arg.Any<CancellationToken>());

        await _accountsClient.DidNotReceive()
            .WithdrawAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAmountBelowThreshold_ShouldProcessNormally()
    {
        // Arrange
        var senderAccount = CreateSenderAccount();
        _accountsClient.GetAccountByIdAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(senderAccount);

        _accountsClient.GetSpendingLimitAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(RequiresApproval: true, ApprovalThreshold: 10_000m, Currency: "CZK"));

        _accountsClient.GetAccountByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AccountInfo?)null);

        _accountsClient.WithdrawAsync(
                _senderAccountId, Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = CreateCommand(amount: 500m); // below 10_000 threshold

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Completed");

        await _accountsClient.DidNotReceive()
            .CreatePendingTransactionAsync(
                Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _accountsClient.Received(1)
            .WithdrawAsync(_senderAccountId, 500m, "CZK",
                Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoSpendingLimit_ShouldProcessNormally()
    {
        // Arrange
        var senderAccount = CreateSenderAccount();
        _accountsClient.GetAccountByIdAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(senderAccount);

        _accountsClient.GetSpendingLimitAsync(_senderAccountId, Arg.Any<CancellationToken>())
            .Returns(new SpendingLimitInfo(RequiresApproval: false, ApprovalThreshold: null, Currency: null));

        _accountsClient.GetAccountByNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AccountInfo?)null);

        _accountsClient.WithdrawAsync(
                _senderAccountId, Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var command = CreateCommand(amount: 5_000m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Completed");

        await _accountsClient.DidNotReceive()
            .CreatePendingTransactionAsync(
                Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        await _accountsClient.Received(1)
            .WithdrawAsync(_senderAccountId, 5_000m, "CZK",
                Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
