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

using Akka.Actor;
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
[Route("api/v1/admin")]
public sealed class AdminServerController : ControllerBase {

    private static readonly DateTime s_startedAtUtc =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();
    private static readonly byte s_gameServerCount =
        Math.Max(ConfigurationManager.Settings["Game Server.GameServerCount"].AsByte(1), (byte) 1);
    private static readonly string[] s_configuredRealmNames =
        ConfigurationManager.Settings["Game Server.RealmNames"].AsList();

    /// <summary>
    /// Returns live server health metrics, memory usage, database totals, and realm populations.
    /// </summary>
    [HttpGet("server/metrics")]
    [Authorize(Policy = ApiScopes.ServerRead)]
    public IActionResult GetMetrics() {
        using var process = Process.GetCurrentProcess();
        var onlinePlayers = OnlinePlayerCollection.GetOnlinePlayers();

        using var session = PlayerDatabase.Instance.Store.OpenSession();

        var totalAccounts = session.Query<Account>(collectionName: AccountCollection.CollectionName).Count();
        var totalWizards = session.Query<Wizard>(collectionName: WizardCollection.CollectionName).Count();

        var realms = new List<object>(s_gameServerCount);
        for (byte i = 0; i < s_gameServerCount; i++) {
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
            startedAtUtc = s_startedAtUtc,
            uptimeSeconds = (long) uptime.TotalSeconds,
            workingSetMb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 2),
            managedHeapMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
            threadCount = process.Threads.Count,
            totalOnlinePlayers = onlinePlayers.Length,
            totalAccounts,
            totalWizards,
            realms
        });
    }

    /// <summary>
    /// Lists all currently connected players enriched with their account and wizard details.
    /// </summary>
    [HttpGet("players/online")]
    [Authorize(Policy = ApiScopes.PlayersRead)]
    public IActionResult GetOnlinePlayers() {
        var onlineEntries = OnlinePlayerCollection.GetOnlinePlayers();
        var result = new List<object>(onlineEntries.Length);
        foreach (var entry in onlineEntries) {
            var account = AccountCollection.GetAccountForCharacterList(entry.AccountId);
            var wizard = account?.Characters.FirstOrDefault(c => c.CharId == entry.CharacterId)
                ?? WizardCollection.GetCharacter(entry.CharacterId);

            result.Add(new {
                sessionId = entry.SessionId,
                accountId = entry.AccountId.ToString(),
                username = account?.Username ?? "Unknown",
                authLevel = account?.AuthLevel.ToString() ?? nameof(AuthLevel.None),
                charId = entry.CharacterId.ToString(),
                wizardName = wizard?.PlayerNameBehavior?.GetWizardName() ?? "Unknown Wizard",
                school = wizard?.MagicSchoolBehavior?.MagicSchool.ToString() ?? "Unknown",
                level = wizard?.MagicSchoolBehavior?.Level ?? 1,
                realm = entry.CurrentRealm ?? "Unknown",
                zone = entry.CurrentZone ?? "Unknown",
                zoneDisplayName = entry.CurrentZoneDisplayName ?? entry.CurrentZone ?? "Unknown"
            });
        }

        return Ok(new { players = result });
    }


}
