// Smart Solar Microgrid Trading System - authentication and prosumer/user workflows.
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using SolarGridX.Api.Common;
using SolarGridX.Api.DTOs.Requests;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Services;

public sealed class UserService(
    MongoContext db,
    IPasswordService passwords,
    ITokenService tokens,
    ILogger<UserService> logger
)
{
    // Registers a Prosumer in pending state so Backoffice can manage activation.
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var nic = NormalizeNic(request.Nic);
        var email = NormalizeEmail(request.Email);
        await EnsureAvailableAsync(nic, email, ct);
        var user = new User
        {
            Nic = nic,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            IsProsumer = true,
            AccountStatus = AccountStatus.Pending,
        };
        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        logger.LogInformation("Prosumer registration created for NIC {Nic}", nic);
        return user.ToResponse();
    }

    // Authenticates an active account and returns a minimal JWT response.
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await db
            .Users.Find(x => x.Email == NormalizeEmail(request.Email))
            .FirstOrDefaultAsync(ct);
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            throw new ApiException(401, "Invalid email or password.");
        if (user.AccountStatus != AccountStatus.Active)
            throw new ApiException(403, "This account is not active.");
        var token = tokens.Create(user);
        logger.LogInformation("Login succeeded for user {UserId}", user.Id);
        return new LoginResponse(token.Token, token.ExpiresAt, user.ToResponse());
    }

    // Finds the current authenticated account from the JWT subject.
    public async Task<UserResponse> GetCurrentAsync(string userId, CancellationToken ct) =>
        (await FindByIdAsync(userId, ct)).ToResponse();

    // Updates permitted profile fields while enforcing self ownership in the controller/service boundary.
    public async Task<UserResponse> UpdateProsumerAsync(
        string nic,
        string callerId,
        bool isBackoffice,
        UpdateProsumerRequest request,
        CancellationToken ct
    )
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (!isBackoffice && user.Id != callerId)
            throw new ApiException(403, "You may only update your own profile.");
        if (request.Email is not null)
        {
            var email = NormalizeEmail(request.Email);
            var conflict = await db
                .Users.Find(x => x.Email == email && x.Id != user.Id)
                .AnyAsync(ct);
            if (conflict)
                throw new ApiException(409, "Email address is already in use.");
            user.Email = email;
        }
        user.FirstName = request.FirstName?.Trim() ?? user.FirstName;
        user.LastName = request.LastName?.Trim() ?? user.LastName;
        user.Phone = request.Phone?.Trim() ?? user.Phone;
        user.Address = request.Address?.Trim() ?? user.Address;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Records the prosumer's request; Backoffice later controls reactivation after deactivation.
    public async Task RequestDeactivationAsync(string nic, string callerId, CancellationToken ct)
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (user.Id != callerId)
            throw new ApiException(403, "You may only request deactivation of your own account.");
        if (user.AccountStatus != AccountStatus.Active)
            throw new ApiException(409, "Only active accounts can request deactivation.");
        await db.Users.UpdateOneAsync(
            x => x.Id == user.Id,
            Builders<User>
                .Update.Set(x => x.AccountStatus, AccountStatus.DeactivationRequested)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct
        );
    }

    // Lets Backoffice activate pending or deactivated Prosumer accounts.
    public async Task<UserResponse> ReactivateAsync(string nic, CancellationToken ct)
    {
        var user = await FindProsumerByNicAsync(nic, ct);
        if (user.AccountStatus == AccountStatus.Active)
            throw new ApiException(409, "Prosumer is already active.");
        user.AccountStatus = AccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Returns pending and deactivation-requested Prosumer accounts to Backoffice.
    public Task<List<UserResponse>> GetPendingAsync(CancellationToken ct) =>
        db
            .Users.Find(x =>
                x.IsProsumer
                && (
                    x.AccountStatus == AccountStatus.Pending
                    || x.AccountStatus == AccountStatus.DeactivationRequested
                )
            )
            .ToListAsync(ct)
            .ContinueWith(t => t.Result.Select(x => x.ToResponse()).ToList(), ct);

    // Creates only Backoffice or GridOperator accounts; Prosumer accounts use public registration.
    public async Task<UserResponse> CreateStaffAsync(
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        var email = NormalizeEmail(request.Email);
        await EnsureAvailableAsync(null, email, ct);
        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = passwords.Hash(request.Password),
            Role = request.Role,
            AccountStatus = AccountStatus.Active,
        };
        await db.Users.InsertOneAsync(user, cancellationToken: ct);
        return user.ToResponse();
    }

    // Retrieves a user or returns a safe not-found response.
    public async Task<User> FindByIdAsync(string id, CancellationToken ct) =>
        await db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "User was not found.");

    // Retrieves a Prosumer by NIC after normalizing the business identifier.
    public async Task<User> FindProsumerByNicAsync(string nic, CancellationToken ct) =>
        await db.Users.Find(x => x.Nic == NormalizeNic(nic) && x.IsProsumer).FirstOrDefaultAsync(ct)
        ?? throw new ApiException(404, "Prosumer was not found.");

    // Avoids duplicate business identifiers before insertion while database unique indexes remain authoritative.
    private async Task EnsureAvailableAsync(string? nic, string email, CancellationToken ct)
    {
        if (await db.Users.Find(x => x.Email == email).AnyAsync(ct))
            throw new ApiException(409, "Email address is already in use.");
        if (nic is not null && await db.Users.Find(x => x.Nic == nic).AnyAsync(ct))
            throw new ApiException(409, "NIC is already registered.");
    }

    // Standardizes email comparisons for unique identity checks.
    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    // Standardizes NIC without imposing an unconfirmed national NIC format.
    private static string NormalizeNic(string value)
    {
        var result = value.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(result))
            throw new ApiException(400, "NIC is required.");
        return result;
    }
}
