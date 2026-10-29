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

using Imlight.Api.Auth;
using Imlight.Api.Data.Collections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Imlight.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tokens")]
[Authorize(Policy = ApiScopes.TokensManage)]
public sealed class AdminTokensController : ControllerBase {

    private static readonly uint s_maxTokenNameLength = 64;

    public sealed record CreateTokenRequest(string Name, List<string> Scopes, int? ExpiresInDays);

    /// <summary>
    /// Returns all existing API tokens (metadata only, never the hash) along with the list of valid scopes.
    /// </summary>
    [HttpGet]
    public IActionResult GetTokens() {
        var tokens = ApiTokenCollection.GetAllTokens()
            .Select(MapToken)
            .ToList();

        return Ok(new {
            tokens,
            availableScopes = ApiScopes.All
        });
    }

    /// <summary>
    /// Generates a new scoped API token, stores its SHA-256 hash in RavenDB, and returns the plaintext key once.
    /// </summary>
    [HttpPost]
    public IActionResult CreateToken([FromBody] CreateTokenRequest request) {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > s_maxTokenNameLength) {
            return BadRequest(new { error = "Token name must be between 1 and 64 characters." });
        }

        if (request.Scopes is null || request.Scopes.Count == 0) {
            return BadRequest(new { error = "At least one scope must be selected." });
        }

        // Filter out any unknown or duplicate scope strings
        var validScopes = request.Scopes
            .Where(s => ApiScopes.All.Contains(s, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (validScopes.Count == 0) {
            return BadRequest(new { error = "None of the requested scopes are valid." });
        }

        DateTime? expiresAt = null;
        if (request.ExpiresInDays is > 0) {
            expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays.Value);
        }

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _ = ulong.TryParse(idClaim, out var createdByAccountId);
        var createdByUsername = User.Identity?.Name ?? "Administrator";

        var (token, plainTextKey) = ApiTokenCollection.CreateToken(
            name,
            validScopes,
            createdByAccountId,
            createdByUsername,
            expiresAt);

        Common.Logger.Information($"API token '{token.Name}' ({token.TokenPrefix}) created by {createdByUsername} with scopes [{string.Join(", ", validScopes)}]");

        return Ok(new {
            token = MapToken(token),
            plainTextKey
        });
    }

    /// <summary>
    /// Soft-revokes an API token by ID so it immediately stops authenticating while staying visible in the audit list.
    /// </summary>
    [HttpDelete("{tokenId}")]
    public IActionResult RevokeToken(ulong tokenId) {
        if (!ApiTokenCollection.RevokeToken(tokenId)) {
            return NotFound(new { error = "Token not found." });
        }

        var caller = User.Identity?.Name ?? "Administrator";
        Common.Logger.Information($"API token {tokenId} revoked by {caller}");

        return Ok(new { message = "Token revoked." });
    }

    /// <summary>
    /// Maps an ApiToken entity to a client-safe DTO without exposing TokenHash.
    /// </summary>
    private static object MapToken(ApiToken t) => new {
        tokenId = t.TokenId.ToString(),
        name = t.Name,
        tokenPrefix = t.TokenPrefix,
        scopes = t.Scopes,
        createdByAccountId = t.CreatedByAccountId.ToString(),
        createdByUsername = t.CreatedByUsername,
        createdAt = t.CreatedAt,
        expiresAt = t.ExpiresAt,
        lastUsedAt = t.LastUsedAt,
        isRevoked = t.IsRevoked,
        isValid = t.IsValid()
    };
}
