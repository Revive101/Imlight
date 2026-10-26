using System;
using System.Collections.Generic;
using System.Text;

namespace Imlight.Api.Data.Collections;

[Serializable]
public class ApiToken {
    public ulong TokenId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public ulong CreatedByAccountId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    public bool IsValid()
        => !IsRevoked && (ExpiresAt is null || ExpiresAt.Value > DateTime.UtcNow);
}
