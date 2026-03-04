using FluentAssertions;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.Enums;
using FairBank.Accounts.Domain.Events;

namespace FairBank.Accounts.UnitTests.Domain;

public class CardTests
{
    [Fact]
    public void Create_ShouldInitializeActiveCard()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);

        card.Id.Should().NotBe(Guid.Empty);
        card.CardNumber.Should().HaveLength(16);
        card.CardNumber.Should().StartWith("4");
        card.HolderName.Should().Be("Jan Novak");
        card.Type.Should().Be(CardType.Debit);
        card.IsActive.Should().BeTrue();
        card.IsFrozen.Should().BeFalse();
        card.OnlinePaymentsEnabled.Should().BeTrue();
        card.ContactlessEnabled.Should().BeTrue();
        card.CVV.Should().HaveLength(3);
    }

    [Fact]
    public void Create_ShouldRaiseCardIssuedEvent()
    {
        var accountId = Guid.NewGuid();
        var card = Card.Create(accountId, "Jan Novak", CardType.Credit, Currency.CZK);

        var events = card.GetUncommittedEvents();
        events.Should().HaveCount(1);
        events[0].Should().BeOfType<CardIssued>();

        var evt = (CardIssued)events[0];
        evt.AccountId.Should().Be(accountId);
        evt.HolderName.Should().Be("Jan Novak");
        evt.Type.Should().Be(CardType.Credit);
    }

    [Fact]
    public void Freeze_ShouldSetFrozenTrue()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.ClearUncommittedEvents();

        card.Freeze();

        card.IsFrozen.Should().BeTrue();
        card.GetUncommittedEvents().Should().HaveCount(1);
        card.GetUncommittedEvents()[0].Should().BeOfType<CardFrozen>();
    }

    [Fact]
    public void Freeze_WhenAlreadyFrozen_ShouldThrow()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.Freeze();

        var act = () => card.Freeze();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already frozen*");
    }

    [Fact]
    public void Unfreeze_ShouldSetFrozenFalse()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.Freeze();
        card.ClearUncommittedEvents();

        card.Unfreeze();

        card.IsFrozen.Should().BeFalse();
        card.GetUncommittedEvents().Should().HaveCount(1);
        card.GetUncommittedEvents()[0].Should().BeOfType<CardUnfrozen>();
    }

    [Fact]
    public void SetLimits_ShouldUpdateLimits()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.ClearUncommittedEvents();

        card.SetLimits(1000m, 5000m, Currency.CZK);

        card.DailyLimit!.Amount.Should().Be(1000m);
        card.MonthlyLimit!.Amount.Should().Be(5000m);
        card.GetUncommittedEvents().Should().HaveCount(1);
        card.GetUncommittedEvents()[0].Should().BeOfType<CardLimitSet>();
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.ClearUncommittedEvents();

        card.Deactivate();

        card.IsActive.Should().BeFalse();
        card.GetUncommittedEvents().Should().HaveCount(1);
        card.GetUncommittedEvents()[0].Should().BeOfType<CardDeactivated>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.Deactivate();

        var act = () => card.Deactivate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*already deactivated*");
    }

    [Fact]
    public void UpdateSettings_ShouldChangeFlags()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);
        card.ClearUncommittedEvents();

        card.UpdateSettings(false, false);

        card.OnlinePaymentsEnabled.Should().BeFalse();
        card.ContactlessEnabled.Should().BeFalse();
        card.GetUncommittedEvents().Should().HaveCount(1);
        card.GetUncommittedEvents()[0].Should().BeOfType<CardSettingsChanged>();
    }

    [Fact]
    public void MaskedNumber_ShouldShowLastFourDigits()
    {
        var card = Card.Create(Guid.NewGuid(), "Jan Novak", CardType.Debit, Currency.CZK);

        var lastFour = card.CardNumber[^4..];
        card.MaskedNumber.Should().Be($"**** **** **** {lastFour}");
    }
}
