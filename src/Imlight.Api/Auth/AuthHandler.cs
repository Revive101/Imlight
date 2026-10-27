using Imlight.Api.Data.Collections;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Imlight.Api.Auth;

public static class JwtTokenService {

    // Both are not necessarily needed, but better be safe than sorry
    public const string Issuer = "Imlight.Api";
    public const string Audience = "Imlight.Api.Clients";


    public static readonly int s_minSecretLength = 32;
    private static readonly int s_lifetimeMinutes = 60 * 12; // 12 hours
    private static readonly byte[] s_signingKey = InitializeKey();

    // JwtSecurtiyTokenHandler is thread-safe. Reuse a single instance
    private static readonly JwtSecurityTokenHandler s_handler = new();

    private static byte[] InitializeKey() {
        var configuredSecret = ConfigurationManager.Settings["API.JwtSecret"].AsString();
        if (!string.IsNullOrWhiteSpace(configuredSecret) && configuredSecret.Length >= s_minSecretLength) {
            return Encoding.UTF8.GetBytes(configuredSecret);
        }

        // Generate a 256-bit key if none is configured in Imlight.ini
        var randomKey = new byte[s_minSecretLength];
        RandomNumberGenerator.Fill(randomKey);
        //TODO: Save key
        return randomKey;
    }

    public static string GenerateToken(Account account) {
        var claims = new List<Claim> {
            // Unique ID of the account. downstream code reads this to know who is calling
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            // Human-readable username, used for logging
            new(ClaimTypes.Name, account.Username),
            // Accounts authorization level (e.g. HallMonitor)
            new("auth_level", account.AuthLevel.ToString()),
            // Unique ID for this specific apiToken, used to revoke or audit individual tokens
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        // Add scopes based on accounts AuthLevel
        foreach (var scope in ApiScopes.GetScopesForAuthLevel(account.AuthLevel)) {
            claims.Add(new Claim("scope", scope));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(s_signingKey),
            SecurityAlgorithms.HmacSha256
        );

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(claims),
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(s_lifetimeMinutes),
            SigningCredentials = credentials
        };

        var token = s_handler.CreateToken(descriptor);
        return s_handler.WriteToken(token);
    }

    /// <summary>
    /// Validates a JWT. Returns null if invalid, expired, revoked or malformed.
    /// </summary>
    public static ClaimsPrincipal? ValidateJwt(string token) =>
        TryValidateJwt(token, out var principal, out _) ? principal : null;

    /// <summary>
    /// Validates a JWT and reports why it was rejected (for logging only, never return it to clients).
    /// </summary>
    public static bool TryValidateJwt(string token, out ClaimsPrincipal? principal, out string? failure) {
        principal = null;
        failure = null;

        try {
            var result = s_handler.ValidateToken(token, new TokenValidationParameters {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(s_signingKey),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            principal = result;
            return true;
        }
        catch (SecurityTokenException ex) {
            failure = ex.GetType().Name;
            return false;
        }
        catch (ArgumentException ex) {
            // Malformed apiToken strings
            failure = ex.GetType().Name;
            return false;
        }
    }
}

public sealed class AuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    public const string SchemeName = "ImlightAuth";

    private static readonly string s_masterToken =
        ConfigurationManager.Settings["API.MasterApiToken"].AsString();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        // Check X-Api-Key header first
        if (Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader)) {
            var rawKey = apiKeyHeader.ToString().Trim();
            return Task.FromResult(AuthenticateApiKey(rawKey));
        }

        // Check Authorization: Bearer <apiToken> (or ?access_token= for SignalR
        string? bearerToken = null;
        if (Request.Headers.TryGetValue("Authorization", out var authHeader)) {
            var headerVal = authHeader.ToString();
            if (headerVal.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
                bearerToken = headerVal["Bearer ".Length..].Trim();
            }
        }
        else if (Request.Query.TryGetValue("access_token", out var queryToken)) {
            bearerToken = queryToken.ToString().Trim();
        }

        if (string.IsNullOrEmpty(bearerToken)) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Support passing `iml_` API tokens in the Bearer header too
        if (bearerToken.StartsWith("iml_", StringComparison.Ordinal)) {
            return Task.FromResult(AuthenticateApiKey(bearerToken));
        }

        if (!JwtTokenService.TryValidateJwt(bearerToken, out var principal, out var failure) || principal is null) {
            Common.Logger.Debug($"JWT rejected from {RemoteIp()}: {failure}");
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired JWT apiToken."));
        }

        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private AuthenticateResult AuthenticateApiKey(string rawKey) {
        if (string.IsNullOrWhiteSpace(rawKey)) {
            return AuthenticateResult.Fail("Empty API key.");
        }

        // Check Master-Token from Imlight.ini
        if (!string.IsNullOrWhiteSpace(s_masterToken) && s_masterToken.Length < JwtTokenService.s_minSecretLength) {
            Common.Logger.Warning($"API.MasterApiToken is configured but shorter than {JwtTokenService.s_minSecretLength} characters, so it is ignored.");
        }

        else if (!string.IsNullOrWhiteSpace(s_masterToken) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(rawKey),
                Encoding.UTF8.GetBytes(s_masterToken))) {
            // Every use of the ALMIGHTY master apiToken is logged
            Common.Logger.Warning($"Master API apiToken used from {RemoteIp()} for {Request.Method} {Request.Path.Value}");


            var masterClaims = new List<Claim> {
                new(ClaimTypes.NameIdentifier, "0"),
                new(ClaimTypes.Name, "MasterToken"),
                new("auth_level", nameof(AuthLevel.Administrator)),
                new("auth_type", "api_token")
            };

            foreach (var scope in ApiScopes.All) {
                masterClaims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(masterClaims, SchemeName);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }

        // Check database for a valid API apiToken
        var apiToken = ApiTokenCollection.ValidateToken(rawKey);
        if (apiToken is null) {
            Common.Logger.Warning($"Rejected invalid, expired or revoked API key from {RemoteIp()}");
            return AuthenticateResult.Fail("Invalid, expired, or revoked API key.");
        }

        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, apiToken.CreatedByAccountId.ToString()),
            new(ClaimTypes.Name, $"Token:{apiToken.Name}"),
            new("token_id", apiToken.TokenId.ToString()),
            new("auth_type", "api_token")
        };

        foreach (var scope in apiToken.Scopes) {
            claims.Add(new Claim("scope", scope));
        }

        var tokenIdentity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(tokenIdentity), SchemeName));
    }

    private string RemoteIp() => Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
