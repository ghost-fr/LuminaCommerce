using Lumina.Domain.Identity;
using Xunit;

namespace Lumina.Domain.Tests.Identity;

public class UserTests
{
    [Fact]
    public void Constructor_Throws_WhenUsernameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new User(Guid.NewGuid(), Guid.NewGuid(), "", "Display Name", "hash"));
    }

    [Fact]
    public void Constructor_Throws_WhenPasswordHashIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new User(Guid.NewGuid(), Guid.NewGuid(), "user1", "Display Name", ""));
    }

    [Fact]
    public void AssignRole_IsIdempotent()
    {
        var user = new User(Guid.NewGuid(), Guid.NewGuid(), "user1", "User One", "hash");
        var roleId = Guid.NewGuid();

        user.AssignRole(roleId);
        user.AssignRole(roleId);

        Assert.Single(user.RoleIds);
    }

    [Fact]
    public void RevokeRole_RemovesAssignedRole()
    {
        var user = new User(Guid.NewGuid(), Guid.NewGuid(), "user1", "User One", "hash");
        var roleId = Guid.NewGuid();
        user.AssignRole(roleId);

        user.RevokeRole(roleId);

        Assert.Empty(user.RoleIds);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = new User(Guid.NewGuid(), Guid.NewGuid(), "user1", "User One", "hash");

        user.Deactivate();

        Assert.False(user.IsActive);
    }
}

public class RoleTests
{
    [Fact]
    public void Grant_AddsCapability()
    {
        var role = new Role(Guid.NewGuid(), Guid.NewGuid(), "Cashier");

        role.Grant(Capabilities.PosOperateRegister);

        Assert.Contains(Capabilities.PosOperateRegister, role.Capabilities);
    }

    [Fact]
    public void Revoke_RemovesCapability()
    {
        var role = new Role(Guid.NewGuid(), Guid.NewGuid(), "Cashier",
            new[] { Capabilities.PosOperateRegister });

        role.Revoke(Capabilities.PosOperateRegister);

        Assert.DoesNotContain(Capabilities.PosOperateRegister, role.Capabilities);
    }
}
