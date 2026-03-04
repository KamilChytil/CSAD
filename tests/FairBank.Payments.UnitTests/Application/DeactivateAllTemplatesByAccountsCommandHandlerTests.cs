using FairBank.Payments.Application.Templates.Commands.DeactivateAllTemplatesByAccounts;
using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using FairBank.Payments.Domain.Ports;
using FairBank.SharedKernel.Application;
using FluentAssertions;
using NSubstitute;

namespace FairBank.Payments.UnitTests.Application;

public class DeactivateAllTemplatesByAccountsCommandHandlerTests
{
    private readonly IPaymentTemplateRepository _repository = Substitute.For<IPaymentTemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeactivateAllTemplatesByAccountsCommandHandler _sut;

    public DeactivateAllTemplatesByAccountsCommandHandlerTests()
    {
        _sut = new DeactivateAllTemplatesByAccountsCommandHandler(_repository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldSoftDeleteAllTemplates_ForGivenAccountIds()
    {
        // Arrange
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        var template1 = PaymentTemplate.Create(accountId1, "Rent", "CZ1234567890", Currency.CZK);
        var template2 = PaymentTemplate.Create(accountId2, "Utilities", "CZ0987654321", Currency.CZK);

        _repository.GetByAccountIdAsync(accountId1, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { template1 });
        _repository.GetByAccountIdAsync(accountId2, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { template2 });

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId1, accountId2]);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        template1.IsDeleted.Should().BeTrue();
        template2.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(template1, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(template2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipAlreadyDeletedTemplates()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var activeTemplate = PaymentTemplate.Create(accountId, "Rent", "CZ1234567890", Currency.CZK);
        var deletedTemplate = PaymentTemplate.Create(accountId, "Old", "CZ0987654321", Currency.CZK);
        deletedTemplate.SoftDelete();

        _repository.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { activeTemplate, deletedTemplate });

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId]);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        activeTemplate.IsDeleted.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(activeTemplate, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateAsync(deletedTemplate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnCountOfDeletedTemplates()
    {
        // Arrange
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();

        var template1 = PaymentTemplate.Create(accountId1, "Rent", "CZ1234567890", Currency.CZK);
        var template2 = PaymentTemplate.Create(accountId1, "Groceries", "CZ1111111111", Currency.CZK);
        var template3 = PaymentTemplate.Create(accountId2, "Gym", "CZ2222222222", Currency.EUR);

        _repository.GetByAccountIdAsync(accountId1, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { template1, template2 });
        _repository.GetByAccountIdAsync(accountId2, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { template3 });

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId1, accountId2]);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldReturnZero_WhenNoTemplatesExistForAccounts()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _repository.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate>());

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId]);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<PaymentTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallSaveChangesAsync_WhenTemplatesAreDeleted()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var template = PaymentTemplate.Create(accountId, "Rent", "CZ1234567890", Currency.CZK);

        _repository.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate> { template });

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId]);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCallSaveChangesAsync_WhenNoTemplatesAreDeleted()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        _repository.GetByAccountIdAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new List<PaymentTemplate>());

        var command = new DeactivateAllTemplatesByAccountsCommand([accountId]);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
