using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData;
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
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
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
