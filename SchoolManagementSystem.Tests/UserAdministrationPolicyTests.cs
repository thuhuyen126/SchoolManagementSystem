using SchoolManagementSystem.Helpers;
using Xunit;

namespace SchoolManagementSystem.Tests;

public sealed class UserAdministrationPolicyTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Employee")]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("Anonymous")]
    [InlineData("Pending")]
    public void AssignableRole_IsAccepted(string role) =>
        Assert.True(UserAdministrationPolicy.IsAssignableRole(role));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("SuperAdmin")]
    public void UnsupportedRole_IsRejected(string? role) =>
        Assert.False(UserAdministrationPolicy.IsAssignableRole(role));

    [Fact]
    public void CannotDeleteSelf()
    {
        Assert.False(UserAdministrationPolicy.CanDeleteUser("user-1", "user-1", false, 2));
    }

    [Theory]
    [InlineData(null, "target")]
    [InlineData("actor", null)]
    [InlineData("", "target")]
    public void CannotDeleteWithoutActorOrTarget(string? actorId, string? targetId) =>
        Assert.False(UserAdministrationPolicy.CanDeleteUser(actorId, targetId, false, 2));

    [Fact]
    public void CannotDeleteLastAdministrator()
    {
        Assert.False(UserAdministrationPolicy.CanDeleteUser("actor", "target", true, 1));
        Assert.False(UserAdministrationPolicy.CanDeleteUser("actor", "target", true, 0));
    }

    [Fact]
    public void CanDeleteNonAdminOrOneOfSeveralAdministrators()
    {
        Assert.True(UserAdministrationPolicy.CanDeleteUser("actor", "target", false, 1));
        Assert.True(UserAdministrationPolicy.CanDeleteUser("actor", "target", true, 2));
    }
}
