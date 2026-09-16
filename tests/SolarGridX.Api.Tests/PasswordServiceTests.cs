// Smart Solar Microgrid Trading System - security service unit tests.
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Tests;

public sealed class PasswordServiceTests
{
    // Confirms a password can verify against its own randomly salted hash.
    [Fact]
    public void Hash_ThenVerify_WithSamePassword_ReturnsTrue()
    {
        var service = new PasswordService();
        var hash = service.Hash("A secure development password");
        Assert.True(service.Verify("A secure development password", hash));
    }

    // Confirms an incorrect password cannot authenticate against a stored hash.
    [Fact]
    public void Verify_WithDifferentPassword_ReturnsFalse()
    {
        var service = new PasswordService();
        var hash = service.Hash("Correct password");
        Assert.False(service.Verify("Incorrect password", hash));
    }

    // Confirms hashing uses a random salt rather than producing a reusable plaintext-equivalent value.
    [Fact]
    public void Hash_WithSamePassword_ProducesDifferentHashes()
    {
        var service = new PasswordService();
        Assert.NotEqual(service.Hash("Same password"), service.Hash("Same password"));
    }
}
