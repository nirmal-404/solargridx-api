// Smart Solar Microgrid Trading System - configuration options.
namespace SolarGridX.Api.Configuration;

public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "SolarGridX";
}
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";
    public GoogleOAuthOptions Google { get; set; } = new();
}
public sealed class GoogleOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool AllowTestTokens { get; set; } = true;
}
public sealed class SeedOptions
{
    public bool Enabled { get; set; }
}

