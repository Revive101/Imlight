/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * Created by: Phill
 * Version: KALI 1.0
 * Last Updated: 29.10.2026
*/

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
        // If it exists in the environment, use the configured key from Imlight.ini
        if (EnvironmentManager.Get("API_JWT_SECRET", out var configuredSecret) && configuredSecret.Length >= s_minSecretLength) {
            return Encoding.UTF8.GetBytes(configuredSecret);
        }


        // Generate a 256-bit key if none is configured in .env
        var secret = Convert.ToHexString(GenerateSecret()).ToLowerInvariant();
        EnvironmentManager.Set("API_JWT_SECRET", secret);

        return Encoding.UTF8.GetBytes(secret);
    }

    private static byte[] GenerateSecret() {
        var secret = new byte[s_minSecretLength];
        RandomNumberGenerator.Fill(secret);
        return secret;
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
