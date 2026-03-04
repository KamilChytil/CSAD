using FairBank.Cards.Application.Commands.DeactivateAllCardsByUser;
using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Enums;
using FairBank.Cards.Domain.Ports;
using FairBank.SharedKernel.Application;
using FluentAssertions;
using NSubstitute;

namespace FairBank.Cards.UnitTests.Application;

public class DeactivateAllCardsByUserCommandHandlerTests
{
    private readonly ICardRepository _cardRepository = Substitute.For<ICardRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeactivateAllCardsByUserCommandHandler _sut;

    public DeactivateAllCardsByUserCommandHandlerTests()
    {
        _sut = new DeactivateAllCardsByUserCommandHandler(_cardRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldBlockAllActiveCards_ForTheUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var card1 = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Debit, CardBrand.Visa);
        var card2 = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Credit, CardBrand.Mastercard);

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card1, card2 });

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        card1.Status.Should().Be(CardStatus.Blocked);
        card2.Status.Should().Be(CardStatus.Blocked);
        await _cardRepository.Received(1).UpdateAsync(card1, Arg.Any<CancellationToken>());
        await _cardRepository.Received(1).UpdateAsync(card2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipCancelledAndExpiredCards()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeCard = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Debit, CardBrand.Visa);

        var cancelledCard = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Debit, CardBrand.Visa);
        cancelledCard.Cancel();

        var expiredCard = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Credit, CardBrand.Mastercard);
        _ = expiredCard.Renew(); // Sets the original card to Expired status

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card> { activeCard, cancelledCard, expiredCard });

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(1);
        activeCard.Status.Should().Be(CardStatus.Blocked);
        cancelledCard.Status.Should().Be(CardStatus.Cancelled);
        expiredCard.Status.Should().Be(CardStatus.Expired);
        await _cardRepository.Received(1).UpdateAsync(activeCard, Arg.Any<CancellationToken>());
        await _cardRepository.DidNotReceive().UpdateAsync(cancelledCard, Arg.Any<CancellationToken>());
        await _cardRepository.DidNotReceive().UpdateAsync(expiredCard, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCountAlreadyBlockedCards_ButNotCallBlockOnThem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeCard = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Debit, CardBrand.Visa);

        var blockedCard = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Credit, CardBrand.Mastercard);
        blockedCard.Block();

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card> { activeCard, blockedCard });

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(2);
        activeCard.Status.Should().Be(CardStatus.Blocked);
        blockedCard.Status.Should().Be(CardStatus.Blocked);
        await _cardRepository.Received(1).UpdateAsync(activeCard, Arg.Any<CancellationToken>());
        await _cardRepository.Received(1).UpdateAsync(blockedCard, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnZero_WhenUserHasNoCards()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _cardRepository.DidNotReceive().UpdateAsync(Arg.Any<Card>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCallSaveChangesAsync_WhenCardsAreBlocked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var card = Card.Issue(Guid.NewGuid(), userId, "John Doe", CardType.Debit, CardBrand.Visa);

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card> { card });

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotCallSaveChangesAsync_WhenNoCardsAreBlocked()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _cardRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        var command = new DeactivateAllCardsByUserCommand(userId);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
