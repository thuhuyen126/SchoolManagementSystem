namespace SchoolManagementSystem.Helpers;

/// <summary>Pure guard rules for actions that can remove administrative access.</summary>
public static class UserAdministrationPolicy
{
    public static IReadOnlyCollection<string> AssignableRoles { get; } =
        Array.AsReadOnly(new[] { "Admin", "Employee", "Student", "Teacher", "Anonymous", "Pending" });

    public static bool IsAssignableRole(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName) &&
        AssignableRoles.Contains(roleName, StringComparer.Ordinal);

    public static bool CanDeleteUser(
        string? actorUserId,
        string? targetUserId,
        bool targetIsAdmin,
        int activeAdminCount)
    {
        if (string.IsNullOrWhiteSpace(actorUserId) ||
            string.IsNullOrWhiteSpace(targetUserId) ||
            string.Equals(actorUserId, targetUserId, StringComparison.Ordinal))
        {
            return false;
        }

        return !targetIsAdmin || activeAdminCount > 1;
    }
}
