using SchoolManagementSystem.Helpers;
using Xunit;

namespace SchoolManagementSystem.Tests;

public sealed class TestMailHelperTests
{
    [Fact]
    public void RejectsUseOutsideTestModeWithoutConnectingToSmtp()
    {
        WithEnvironment(
            ("SCHOOL_TEST_MODE", "false"),
            ("TEST_EMAIL_MODE", null),
            () => Assert.Throws<InvalidOperationException>(
                () => new TestMailHelper().SendEmail("test@example.invalid", "subject", "body")));
    }

    [Fact]
    public void RejectsNonLocalEmailModeWithoutConnectingToSmtp()
    {
        WithEnvironment(
            ("SCHOOL_TEST_MODE", "true"),
            ("TEST_EMAIL_MODE", "external"),
            () => Assert.Throws<InvalidOperationException>(
                () => new TestMailHelper().SendEmail("test@example.invalid", "subject", "body")));
    }

    private static void WithEnvironment(
        (string Name, string? Value) first,
        (string Name, string? Value) second,
        Action action)
    {
        var originalFirst = Environment.GetEnvironmentVariable(first.Name);
        var originalSecond = Environment.GetEnvironmentVariable(second.Name);
        try
        {
            Environment.SetEnvironmentVariable(first.Name, first.Value);
            Environment.SetEnvironmentVariable(second.Name, second.Value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(first.Name, originalFirst);
            Environment.SetEnvironmentVariable(second.Name, originalSecond);
        }
    }
}
