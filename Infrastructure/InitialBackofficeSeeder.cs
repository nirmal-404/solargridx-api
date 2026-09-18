// Smart Solar Microgrid Trading System - one-time Backoffice bootstrap account.
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;
using SolarGridX.Api.Services;

namespace SolarGridX.Api.Infrastructure;

public sealed class InitialBackofficeSeeder(
    MongoContext db,
    IPasswordService passwords,
    IOptions<SeedOptions> options,
    ILogger<InitialBackofficeSeeder> logger
)
{
    // Creates the configured first Backoffice only when no Backoffice exists yet.
    // Subsequent staff accounts must be created through the authenticated API.
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var seed = options.Value;
        if (!seed.Enabled)
            return;

        if (await db.Users.Find(x => x.Role == UserRole.Backoffice).AnyAsync(ct))
        {
            logger.LogInformation("Initial Backoffice seed skipped because a Backoffice account already exists.");
            return;
        }

        var email = seed.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(seed.Password)
            || seed.Password.Length < 8
            || string.IsNullOrWhiteSpace(seed.FirstName)
            || string.IsNullOrWhiteSpace(seed.LastName))
        {
            throw new InvalidOperationException(
                "When Seed:Enabled is true, configure Seed:Email, Seed:Password (at least 8 characters), Seed:FirstName and Seed:LastName."
            );
        }

        var user = new User
        {
            Email = email,
            PasswordHash = passwords.Hash(seed.Password),
            FirstName = seed.FirstName.Trim(),
            LastName = seed.LastName.Trim(),
            Role = UserRole.Backoffice,
            AccountStatus = AccountStatus.Active,
        };

        try
        {
            await db.Users.InsertOneAsync(user, cancellationToken: ct);
            logger.LogInformation("Initial Backoffice account was created for {Email}.", email);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another application instance may have completed this one-time seed concurrently.
            if (await db.Users.Find(x => x.Role == UserRole.Backoffice).AnyAsync(ct))
            {
                logger.LogInformation("Initial Backoffice seed was completed by another application instance.");
                return;
            }

            throw new InvalidOperationException(
                "The configured Seed:Email is already in use by a non-Backoffice account. Choose a different Seed:Email."
            );
        }
    }
}
