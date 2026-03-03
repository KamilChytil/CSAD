using FairBank.Identity.Domain.Enums;
using FairBank.Identity.Domain.ValueObjects;
using FairBank.SharedKernel.Domain;

namespace FairBank.Identity.Domain.Entities;

public sealed class User : AggregateRoot<Guid>
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? ParentId { get; private set; }
    public User? Parent { get; private set; }
    private readonly List<User> _children = [];
    public IReadOnlyCollection<User> Children => _children.AsReadOnly();

    private User() { } // EF Core

    public static User Create(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));
        ArgumentNullException.ThrowIfNull(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateChild(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        Guid parentId)
    {
        var child = Create(firstName, lastName, email, passwordHash, Enums.UserRole.Child);
        child.ParentId = parentId;
        return child;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        IsActive = true;
        DeletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
