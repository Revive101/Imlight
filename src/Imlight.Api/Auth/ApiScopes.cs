using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Text;

namespace Imlight.Api.Auth;

public static class ApiScopes {
    public const string DashboardRead = "dashboard:read";
    public const string ServerRead = "server:read";
    public const string PlayersRead = "players:read";
    public const string ModerationWrite = "moderation:write";
    public const string CharactersRead = "characters:read";
    public const string CharactersWrite = "characters:write";
    public const string ConsoleRead = "console:read";
    public const string AccountsRead = "accounts:read";
    public const string AccountsWrite = "accounts:write";
    public const string TokensManage = "tokens:manage";

    public static readonly string[] All = [
        DashboardRead,
        ServerRead,
        PlayersRead,
        ModerationWrite,
        CharactersRead,
        CharactersWrite,
        ConsoleRead,
        AccountsRead,
        AccountsWrite,
        TokensManage
    ];

    /// <summary>
    /// Maps an account's AuthLevel to the equivalent API scopes.
    /// </summary>
    public static List<string> GetScopesForAuthLevel(AuthLevel level) {
        var scopes = new List<string> { DashboardRead };

        if (level >= AuthLevel.HallMonitor) {
            scopes.Add(ServerRead);
            scopes.Add(PlayersRead);
            scopes.Add(ModerationWrite);
        }

        if (level >= AuthLevel.Developer) {
            scopes.Add(CharactersRead);
            scopes.Add(CharactersWrite);
            scopes.Add(ConsoleRead);
        }

        if (level >= AuthLevel.Administrator) {
            scopes.Add(AccountsRead);
            scopes.Add(AccountsWrite);
            scopes.Add(TokensManage);
        }

        return scopes;
    }

}
