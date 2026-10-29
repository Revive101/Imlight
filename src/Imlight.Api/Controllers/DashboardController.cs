using Imlight.Api.Auth;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Imlight.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = ApiScopes.DashboardRead)]
public sealed class DashboardController : ControllerBase {

    private static readonly DateTime s_startedAtUtc =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private static readonly byte s_gameServerCount =
        Math.Max(ConfigurationManager.Settings["Game Server.GameServerCount"].AsByte(1), (byte) 1);

    private static readonly string[] s_configuredRealmNames =
        ConfigurationManager.Settings["Game Server.RealmNames"].AsList();

    /// <summary>
    /// Returns live server status, uptime, total online players, and per-realm population.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() {
        var onlinePlayers = OnlinePlayerCollection.GetOnlinePlayers();
        var realms = new List<object>(s_gameServerCount);

        for (byte i = 0; i < s_gameServerCount; i++) {
            // Maybe define the count of reals by their names and not names (or alternative numbers)
            var realmName = i < s_configuredRealmNames.Length
                ? s_configuredRealmNames[i]
                : $"Realm-{i + 1}";

            var count = onlinePlayers.Count(p =>
                string.Equals(p.CurrentRealm, realmName, StringComparison.OrdinalIgnoreCase));

            realms.Add(new {
                name = realmName,
                onlineCount = count
            });
        }

        var uptime = DateTime.UtcNow - s_startedAtUtc;

        return Ok(new {
            online = true,
            startedAtUtc = s_startedAtUtc,
            uptimeSeconds = (long) uptime.TotalSeconds,
            totalOnlinePlayers = onlinePlayers.Length,
            realms
        });
    }

    /// <summary>
    /// Returns the top wizards across the server ranked by level/XP and by gold.
    /// </summary>
    [HttpGet("leaderboards")]
    public IActionResult GetLeaderboards([FromQuery] int limit = 25) {
        limit = Math.Clamp(limit, 1, 100);
        using var session = PlayerDatabase.Instance.Store.OpenSession();
        // Load wizards for leaderboard ranking
        var wizards = session.Query<Wizard>(collectionName: WizardCollection.CollectionName)
            .ToList();
        var topByLevel = wizards
            .OrderByDescending(w => w.MagicSchoolBehavior?.Level ?? 1)
            .ThenByDescending(w => w.MagicSchoolBehavior?.ExperiencePoints ?? 0)
            .Take(limit)
            .Select((w, idx) => MapLeaderboardEntry(w, idx + 1))
            .ToList();
        var topByGold = wizards
            .OrderByDescending(w => w.GameStats?.m_currentGold ?? 0)
            .ThenByDescending(w => w.MagicSchoolBehavior?.Level ?? 1)
            .Take(limit)
            .Select((w, idx) => MapLeaderboardEntry(w, idx + 1))
            .ToList();
        return Ok(new {
            topByLevel,
            topByGold
        });
    }

    /// <summary>
    /// Maps a Wizard entity into a public leaderboard row (never exposing AccountId or sensitive fields).
    /// </summary>
    private static object MapLeaderboardEntry(Wizard w, int rank) => new {
        rank,
        charId = w.CharId.ToString(),
        name = w.PlayerNameBehavior?.GetWizardName() ?? "Unknown Wizard",
        school = w.MagicSchoolBehavior?.MagicSchool.ToString() ?? "Unknown",
        level = w.MagicSchoolBehavior?.Level ?? 1,
        experience = w.MagicSchoolBehavior?.ExperiencePoints ?? 0,
        gold = w.GameStats?.m_currentGold ?? 0
    };
}
