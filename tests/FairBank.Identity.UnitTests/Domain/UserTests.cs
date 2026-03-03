using FluentAssertions;
using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;

namespace FairBank.Identity.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUser()
    {
        var user = User.Create(
            firstName: "Jan",
            lastName: "Novák",
            email: Email.Create("jan@example.com"),
            passwordHash: "hashed_password_123",
            role: UserRole.Client);

        user.Id.Should().NotBe(Guid.Empty);
        user.FirstName.Should().Be("Jan");
        user.LastName.Should().Be("Novák");
        user.Email.Value.Should().Be("jan@example.com");
        user.Role.Should().Be(UserRole.Client);
        user.IsActive.Should().BeTrue();
        user.IsDeleted.Should().BeFalse();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyFirstName_ShouldThrow()
    {
        var act = () => User.Create("", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_ShouldMarkAsDeleted()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);

        user.SoftDelete();

        user.IsDeleted.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        user.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Restore_ShouldUnmarkDeleted()
    {
        var user = User.Create("Jan", "Novák", Email.Create("jan@example.com"), "hash", UserRole.Client);
        user.SoftDelete();

        user.Restore();

        user.IsDeleted.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.DeletedAt.Should().BeNull();
    }
}
