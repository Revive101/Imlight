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
 * API Token Collection for managing API tokens in the database.
 * ========================================================================
 *
 * Created by: Phill
 * Version: KALI 1.0
 * Last Updated: 29.10.2026
*/

using Imlight.Api.Auth;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Databases;
using Raven.Client.Documents;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.Api.Data.Collections;

public static class ApiTokenCollection {

    public const string CollectionName = "ApiTokens";
    private static readonly IDocumentStore s_store;

    static ApiTokenCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Creates a new scoped API token, stores its hash in the database, and returns both the entity and the one-time plaintext token.
    /// </summary>
    public static (ApiToken Tiken, string PlainTextKey) CreateToken(
        string name,
        List<string> scopes,
        ulong createdByAccountId,
        string createdByUsername,
        DateTime? expiresAt = null) {

        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(JwtTokenService.s_minSecretLength)).ToLowerInvariant();
        var plaintextKey = $"iml_{prefix}_{secret}";

        var token = new ApiToken {
            TokenId = RandomGen.GenerateGUID(),
            Name = name,
            TokenPrefix = $"iml_{prefix}",
            TokenHash = HashToken(plaintextKey),
            Scopes = scopes ?? [],
            CreatedByAccountId = createdByAccountId,
            CreatedByUsername = createdByUsername,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        using var session = s_store.OpenSession();
        session.Store(token);

        var metadata = session.Advanced.GetMetadataFor(token);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        session.SaveChanges();

        return (token, plaintextKey);
    }

    /// <summary>
    /// Validates a plaintext API key and updates its LastUsedAt timestamp if valid.
    /// </summary>
    public static ApiToken? ValidateToken(string plaintextKey) {
        if (string.IsNullOrWhiteSpace(plaintextKey)) {
            return null;
        }

        var tokenHash = HashToken(plaintextKey);

        using var session = s_store.OpenSession();
        var token = session.Query<ApiToken>(collectionName: CollectionName)
            .FirstOrDefault(t => t.TokenHash == tokenHash);

        if (token is null || !token.IsValid()) {
            return null;
        }

        token.LastUsedAt = DateTime.UtcNow;
        session.SaveChanges();

        return token;
    }

    public static List<ApiToken> GetAllTokens() {
        using var session = s_store.OpenSession();
        return session.Query<ApiToken>(collectionName: CollectionName)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public static bool RevokeToken(ulong tokenId) {
        using var session = s_store.OpenSession();
        var token = session.Query<ApiToken>(collectionName: CollectionName)
            .FirstOrDefault(t => t.TokenId == tokenId);

        if (token is null) {
            return false;
        }

        token.IsRevoked = true;
        session.SaveChanges();
        return true;
    }

    private static string HashToken(string plaintextKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey)));
}
