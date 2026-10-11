using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Controllers;
using SchoolManagementSystem.Models;
using System.Reflection;
using Xunit;

namespace SchoolManagementSystem.Tests;

public sealed class AlertControllerSecurityTests
{
    [Fact]
    public void CreatePost_RequiresAuthenticationAndAntiforgery()
    {
        var action = typeof(AlertController).GetMethod(nameof(AlertController.Create), new[] { typeof(AlertViewModel) });

        Assert.NotNull(action);
        Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void MarkAsResolved_RequiresAdminAndAntiforgery()
    {
        var action = typeof(AlertController).GetMethod(nameof(AlertController.MarkAsResolved), new[] { typeof(int) });

        Assert.NotNull(action);
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize.Roles);
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
