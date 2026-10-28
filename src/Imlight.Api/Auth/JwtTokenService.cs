using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
