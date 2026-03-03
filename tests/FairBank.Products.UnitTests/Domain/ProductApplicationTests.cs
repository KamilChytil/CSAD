using FluentAssertions;
using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;

namespace FairBank.Products.UnitTests.Domain;

public class ProductApplicationTests
{
    private static ProductApplication CreateValid(
        Guid? userId = null,
        ProductType productType = ProductType.PersonalLoan,
        string parameters = "{\"amount\":200000}",
        decimal monthlyPayment = 5000m)
    {
        return ProductApplication.Create(
            userId ?? Guid.NewGuid(), productType, parameters, monthlyPayment);
    }

    // ── Create ──────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParams_ShouldReturnPendingApplication()
    {
        var userId = Guid.NewGuid();

        var app = ProductApplication.Create(userId, ProductType.PersonalLoan, "{}", 1000m);

        app.UserId.Should().Be(userId);
        app.ProductType.Should().Be(ProductType.PersonalLoan);
        app.Status.Should().Be(ApplicationStatus.Pending);
        app.Parameters.Should().Be("{}");
        app.MonthlyPayment.Should().Be(1000m);
    }

    [Fact]
    public void Create_ShouldGenerateNonEmptyId()
    {
        var app = CreateValid();
        app.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ShouldSetCreatedAtCloseToUtcNow()
    {
        var before = DateTime.UtcNow;
        var app = CreateValid();
        var after = DateTime.UtcNow;

        app.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_ShouldHaveNullReviewFields()
    {
        var app = CreateValid();

        app.ReviewedAt.Should().BeNull();
        app.ReviewedBy.Should().BeNull();
        app.Note.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        var act = () => ProductApplication.Create(Guid.Empty, ProductType.PersonalLoan, "{}", 1000m);
        act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Create_WithNullParameters_ShouldThrowArgumentException()
    {
        var act = () => ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, null!, 1000m);
        act.Should().Throw<ArgumentException>().WithMessage("*Parameters*");
    }

    [Fact]
    public void Create_WithEmptyParameters_ShouldThrowArgumentException()
    {
        var act = () => ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "", 1000m);
        act.Should().Throw<ArgumentException>().WithMessage("*Parameters*");
    }

    [Fact]
    public void Create_WithWhitespaceParameters_ShouldThrowArgumentException()
    {
        var act = () => ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "   ", 1000m);
        act.Should().Throw<ArgumentException>().WithMessage("*Parameters*");
    }

    [Fact]
    public void Create_WithNegativeMonthlyPayment_ShouldThrowArgumentException()
    {
        var act = () => ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "{}", -1m);
        act.Should().Throw<ArgumentException>().WithMessage("*MonthlyPayment*");
    }

    [Fact]
    public void Create_WithZeroMonthlyPayment_ShouldSucceed()
    {
        var app = ProductApplication.Create(Guid.NewGuid(), ProductType.PersonalLoan, "{}", 0m);
        app.MonthlyPayment.Should().Be(0m);
        app.Status.Should().Be(ApplicationStatus.Pending);
    }

    [Theory]
    [InlineData(ProductType.PersonalLoan)]
    [InlineData(ProductType.Mortgage)]
    [InlineData(ProductType.TravelInsurance)]
    [InlineData(ProductType.PropertyInsurance)]
    [InlineData(ProductType.LifeInsurance)]
    [InlineData(ProductType.PaymentProtection)]
    public void Create_WithAnyProductType_ShouldSetCorrectType(ProductType type)
    {
        var app = ProductApplication.Create(Guid.NewGuid(), type, "{}", 100m);
        app.ProductType.Should().Be(type);
    }

    // ── Approve ─────────────────────────────────────────────

    [Fact]
    public void Approve_WhenPending_ShouldSetStatusToActive()
    {
        var app = CreateValid();
        var reviewerId = Guid.NewGuid();

        app.Approve(reviewerId);

        app.Status.Should().Be(ApplicationStatus.Active);
    }

    [Fact]
    public void Approve_ShouldSetReviewerFields()
    {
        var app = CreateValid();
        var reviewerId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        app.Approve(reviewerId, "Looks good");

        app.ReviewedBy.Should().Be(reviewerId);
        app.Note.Should().Be("Looks good");
        app.ReviewedAt.Should().NotBeNull();
        app.ReviewedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Approve_WithoutNote_ShouldLeaveNoteNull()
    {
        var app = CreateValid();
        app.Approve(Guid.NewGuid());
        app.Note.Should().BeNull();
    }

    [Theory]
    [InlineData(ApplicationStatus.Active)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Cancelled)]
    public void Approve_WhenNotPending_ShouldThrowInvalidOperationException(ApplicationStatus status)
    {
        var app = CreateValid();
        TransitionTo(app, status);

        var act = () => app.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Cannot approve*{status}*");
    }

    // ── Reject ──────────────────────────────────────────────

    [Fact]
    public void Reject_WhenPending_ShouldSetStatusToRejected()
    {
        var app = CreateValid();
        var reviewerId = Guid.NewGuid();

        app.Reject(reviewerId, "Insufficient income");

        app.Status.Should().Be(ApplicationStatus.Rejected);
        app.ReviewedBy.Should().Be(reviewerId);
        app.Note.Should().Be("Insufficient income");
    }

    [Theory]
    [InlineData(ApplicationStatus.Active)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Cancelled)]
    public void Reject_WhenNotPending_ShouldThrowInvalidOperationException(ApplicationStatus status)
    {
        var app = CreateValid();
        TransitionTo(app, status);

        var act = () => app.Reject(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Cannot reject*{status}*");
    }

    // ── Cancel ──────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenPending_ShouldSetStatusToCancelled()
    {
        var app = CreateValid();

        app.Cancel();

        app.Status.Should().Be(ApplicationStatus.Cancelled);
    }

    [Theory]
    [InlineData(ApplicationStatus.Active)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Cancelled)]
    public void Cancel_WhenNotPending_ShouldThrowInvalidOperationException(ApplicationStatus status)
    {
        var app = CreateValid();
        TransitionTo(app, status);

        var act = () => app.Cancel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Cannot cancel*{status}*");
    }

    // ── Double transitions ──────────────────────────────────

    [Fact]
    public void Approve_ThenReject_ShouldThrow()
    {
        var app = CreateValid();
        app.Approve(Guid.NewGuid());

        var act = () => app.Reject(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_ThenCancel_ShouldThrow()
    {
        var app = CreateValid();
        app.Approve(Guid.NewGuid());

        var act = () => app.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_ThenApprove_ShouldThrow()
    {
        var app = CreateValid();
        app.Reject(Guid.NewGuid());

        var act = () => app.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ThenApprove_ShouldThrow()
    {
        var app = CreateValid();
        app.Cancel();

        var act = () => app.Approve(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Helper ──────────────────────────────────────────────

    private static void TransitionTo(ProductApplication app, ApplicationStatus status)
    {
        switch (status)
        {
            case ApplicationStatus.Active:
                app.Approve(Guid.NewGuid());
                break;
            case ApplicationStatus.Rejected:
                app.Reject(Guid.NewGuid());
                break;
            case ApplicationStatus.Cancelled:
                app.Cancel();
                break;
        }
    }
}
