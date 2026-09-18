// Smart Solar Microgrid Trading System - User authentication and account validation tests.
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SolarGridX.Api.Common;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.Middleware;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Tests;

public sealed class UserAuthIntegrationTests
{
    private readonly PasswordService _passwords = new();

    [Fact]
    public void PasswordService_VerifiesNewPasswordProperly()
    {
        var originalPassword = "InitialPassword123!";
        var newPassword = "UpdatedSecurePassword456#";

        var originalHash = _passwords.Hash(originalPassword);
        Assert.True(_passwords.Verify(originalPassword, originalHash));

        var newHash = _passwords.Hash(newPassword);
        Assert.True(_passwords.Verify(newPassword, newHash));
        Assert.False(_passwords.Verify(originalPassword, newHash));
    }

    [Fact]
    public void AccountStatus_Enums_AreProperlyDifferentiated()
    {
        Assert.Equal(AccountStatus.Pending, (AccountStatus)0);
        Assert.Equal(AccountStatus.Active, (AccountStatus)1);
        Assert.Equal(AccountStatus.DeactivationRequested, (AccountStatus)2);
        Assert.Equal(AccountStatus.Deactivated, (AccountStatus)3);
    }
}
