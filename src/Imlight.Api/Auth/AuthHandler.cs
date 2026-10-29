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
 * ========================================================================
 * Authentication handler
 * ========================================================================
 *
 * Created by: Phill
 * Version: KALI 1.0
 * Last Updated: 29.10.2026
*/

using Imlight.Api.Data.Collections;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Imlight.Api.Auth;

public sealed class AuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    public const string SchemeName = "ImlightAuth";

    private static readonly string s_masterToken =
        EnvironmentManager.Settings["API_MASTER_KEY"].AsString();

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
            Common.Logger.Warning($"API_MASTER_KEY is configured but shorter than {JwtTokenService.s_minSecretLength} characters.");
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
