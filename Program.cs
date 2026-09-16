// Smart Solar Microgrid Trading System - application composition root.
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SolarGridX.Api.Configuration;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Middleware;
using SolarGridX.Api.Services;

DotEnvConfiguration.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<MongoDbOptions>(builder.Configuration.GetSection(MongoDbOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt =
    builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
{
    throw new InvalidOperationException("Configure Jwt:Secret with at least 32 characters through user secrets or environment variables.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "SolarGridX API",
            Version = "v1",
        }
    );
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        }
    );
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(
                    new SolarGridX.Api.DTOs.Responses.ErrorResponse(
                        StatusCodes.Status401Unauthorized,
                        "Authentication is required or the access token is invalid.",
                        null,
                        DateTime.UtcNow
                    )
                );
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(
                    new SolarGridX.Api.DTOs.Responses.ErrorResponse(
                        StatusCodes.Status403Forbidden,
                        "You do not have permission to perform this operation.",
                        null,
                        DateTime.UtcNow
                    )
                );
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "WebClient",
        policy =>
            policy
                .WithOrigins(
                    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
    );
});
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<StationSlotService>();
builder.Services.AddScoped<ReservationService>();

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet(
        "/health",
        async (MongoContext db, CancellationToken ct) =>
        {
            await db.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
                cancellationToken: ct
            );

            return Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
        }
    )
    .AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<MongoContext>().InitializeIndexesAsync();
}

app.Run();

public partial class Program { }
