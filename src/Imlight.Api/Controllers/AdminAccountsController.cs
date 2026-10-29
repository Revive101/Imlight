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
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imlight.Api.Controllers;

[ApiController]
[Route("api/v1/admin/accounts")]
public sealed class AdminAccountsController(ActorSystem actorSystem) : ControllerBase {
    public sealed record AddInfractionRequest(string Type, string Reason, int? DurationMinutes);
    public sealed record SetLockRequest(bool IsLocked);
    public sealed record SetCrownsRequest(int Crowns);
    public sealed record SetAuthLevelRequest(string AuthLevel);
    public sealed record TeleportRequest(string ZoneName, string? Location = null);

    /// <summary>
    /// Searches or lists accounts (Can be used by HallMonitor for moderation lookup).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = ApiScopes.PlayersRead)]
    public IActionResult SearchAccounts([FromQuery] string? q = null, [FromQuery] int limit = 50) {
        limit = Math.Clamp(limit, 1, 200);
        var queryText = q?.Trim() ?? string.Empty;

        using var session = PlayerDatabase.Instance.Store.OpenSession();
        var allAccounts = session.Query<Account>(collectionName: AccountCollection.CollectionName).ToList();

        var filtered = string.IsNullOrWhiteSpace(queryText)
            ? allAccounts
            : allAccounts.Where(a =>
                (a.Username?.Contains(queryText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                a.AccountId.ToString().Contains(queryText, StringComparison.Ordinal));

        var onlineByAccount = OnlinePlayerCollection.GetOnlinePlayers()
            .ToDictionary(p => p.AccountId, p => p);

        var results = filtered
         .OrderByDescending(a => a.CreationTime)
         .Take(limit)
         .Select(a => {
             onlineByAccount.TryGetValue(a.AccountId, out var online);
             return new {
                 accountId = a.AccountId.ToString(),
                 username = a.Username,
                 authLevel = a.AuthLevel.ToString(),
                 chatMode = a.ChatMode.ToString(),
                 crowns = a.Crowns,
                 isLocked = a.IsLocked,
                 characterCount = a.CharacterIds.Count,
                 infractionCount = a.InfractionIds.Count,
                 creationTime = a.CreationTime,
                 lastLoginTime = a.LastLoginTime,
                 isOnline = online is not null,
                 onlineRealm = online?.CurrentRealm,
                 onlineZone = online?.CurrentZone
             };
         })
         .ToList();

        return Ok(new { accounts = results });
    }

    /// <summary>
    /// Returns full details for a single account, including its characters and infraction history.
    /// </summary>
    [HttpGet("{accountId}")]
    [Authorize(Policy = ApiScopes.PlayersRead)]
    public IActionResult GetAccountDetails(ulong accountId) {
        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null) {
            return NotFound(new { error = "Account not found." });
        }

        var online = OnlinePlayerCollection.GetOnlinePlayers()
            .FirstOrDefault(p => p.AccountId == accountId);

        var characters = account.Characters.Select(w => new {
            charId = w.CharId.ToString(),
            name = w.PlayerNameBehavior?.GetWizardName() ?? "Unknown Wizard",
            school = w.MagicSchoolBehavior?.MagicSchool.ToString() ?? "Unknown",
            level = w.MagicSchoolBehavior?.Level ?? 1,
            gold = w.GameStats?.m_currentGold ?? 0,
            zone = w.Zone ?? "Unknown",
            isOnline = online?.CharacterId == w.CharId
        }).ToList();

        var infractions = (account.InfractionHistory?.Infractions ?? [])
            .OrderByDescending(i => i.InfractionTime)
            .Select(i => new {
                infractionId = i.InfractionId.ToString(),
                type = i.InfractionType.ToString(),
                reason = i.Reason,
                infractionTime = i.InfractionTime,
                expiration = i.Expiration,
                isExpired = i.IsExpired,
                wasWaived = i.WasWaived,
                wasWaivedBy = i.WasWaivedBy,
                moderator = i.ResponsibleModerator
            })
            .ToList();

        var isMuted = (account.InfractionHistory?.Infractions ?? [])
            .Any(i => i.InfractionType == InfractionType.Mute && !i.IsExpired && !i.WasWaived);

        return Ok(new {
            accountId = account.AccountId.ToString(),
            username = account.Username,
            authLevel = account.AuthLevel.ToString(),
            chatMode = account.ChatMode.ToString(),
            crowns = account.Crowns,
            isLocked = account.IsLocked,
            isBanned = account.InfractionHistory?.IsCurrentlyBanned ?? false,
            isMuted,
            creationTime = account.CreationTime,
            lastLoginTime = account.LastLoginTime,
            lastLoginIp = account.LastLoginIp,
            isOnline = online is not null,
            onlineSessionId = online?.SessionId,
            onlineRealm = online?.CurrentRealm,
            onlineZone = online?.CurrentZone,
            characters,
            infractions
        });
    }

    /// <summary>
    /// Kicks the currently active session for this account if the player is online.
    /// </summary>
    [HttpPost("{accountId}/kick")]
    [Authorize(Policy = ApiScopes.ModerationWrite)]
    public IActionResult KickAccount(ulong accountId) {
        if (!KickOnlineAccountIfConnected(accountId)) {
            return NotFound(new { error = "Account has no active online session." });
        }

        var caller = User.Identity?.Name ?? "Staff";
        Common.Logger.Information($"Account {accountId} kicked from server by {caller}");

        return Ok(new { message = "Player kicked from server." });
    }

    /// <summary>
    /// Issues a Warn, Mute, or Ban infraction to the account (and kicks them immediately if banned while online).
    /// </summary>
    [HttpPost("{accountId}/infractions")]
    [Authorize(Policy = ApiScopes.ModerationWrite)]
    public IActionResult AddInfraction(ulong accountId, [FromBody] AddInfractionRequest request) {
        if (!Enum.TryParse<InfractionType>(request.Type, ignoreCase: true, out var infractionType)) {
            return BadRequest(new { error = "Invalid infraction type. Use Warn, Mute, or Ban." });
        }

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason)) {
            return BadRequest(new { error = "A reason is required for all infractions." });
        }

        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null) {
            return NotFound(new { error = "Account not found." });
        }

        DateTime? expiration = request.DurationMinutes is > 0
            ? DateTime.UtcNow.AddMinutes(request.DurationMinutes.Value)
            : null;

        var moderator = User.Identity?.Name ?? "Staff";
        var infraction = account.AddInfraction(infractionType, reason, moderator, expiration);

        // If this was a Ban, immediately kick the player if they are currently in-game
        if (infractionType == InfractionType.Ban) {
            KickOnlineAccountIfConnected(accountId);
        }

        Common.Logger.Information(
            $"{infractionType} issued to '{account.Username}' ({accountId}) by {moderator}: {reason}");

        return Ok(new {
            message = $"{infractionType} issued to {account.Username}.",
            infractionId = infraction.InfractionId.ToString()
        });
    }

    /// <summary>
    /// Waives an active Ban or Mute infraction by its ID.
    /// </summary>
    [HttpPost("{accountId}/infractions/{infractionId}/waive")]
    [Authorize(Policy = ApiScopes.ModerationWrite)]
    public IActionResult WaiveInfraction(ulong accountId, ulong infractionId) {
        var infraction = InfractionCollection.GetInfraction(infractionId);
        if (infraction is null || infraction.AccountId != accountId) {
            return NotFound(new { error = "Infraction not found for this account." });
        }

        if (infraction.IsExpired || infraction.WasWaived) {
            return BadRequest(new { error = "Infraction is already expired or waived." });
        }

        var moderator = User.Identity?.Name ?? "Staff";
        infraction.WasWaived = true;
        infraction.WasWaivedBy = moderator;
        InfractionCollection.UpdateInfraction(infraction);

        Common.Logger.Information($"Infraction {infractionId} on account {accountId} waived by {moderator}");

        return Ok(new { message = "Infraction waived." });
    }

    /// <summary>
    /// Locks or unlocks an account (and kicks them immediately if locked while online).
    /// </summary>
    [HttpPost("{accountId}/lock")]
    [Authorize(Policy = ApiScopes.ModerationWrite)]
    public IActionResult SetLock(ulong accountId, [FromBody] SetLockRequest request) {
        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null) {
            return NotFound(new { error = "Account not found." });
        }

        var ok = request.IsLocked
            ? AccountCollection.LockAccount(account.Username)
            : AccountCollection.UnlockAccount(account.Username);

        if (!ok) {
            return StatusCode(500, new { error = "Failed to update account lock state." });
        }

        if (request.IsLocked) {
            KickOnlineAccountIfConnected(accountId);
        }

        var caller = User.Identity?.Name ?? "Staff";
        Common.Logger.Information($"Account '{account.Username}' lock set to {request.IsLocked} by {caller}");

        return Ok(new { message = request.IsLocked ? "Account locked." : "Account unlocked." });
    }

    /// <summary>
    /// Updates an account's Crowns balance (Administrator only).
    /// </summary>
    [HttpPut("{accountId}/crowns")]
    [Authorize(Policy = ApiScopes.AccountsWrite)]
    public IActionResult SetCrowns(ulong accountId, [FromBody] SetCrownsRequest request) {
        if (request.Crowns < 0) {
            return BadRequest(new { error = "Crowns cannot be negative." });
        }

        if (!AccountCollection.UpdateCrowns(accountId, request.Crowns)) {
            return NotFound(new { error = "Account not found." });
        }

        var caller = User.Identity?.Name ?? "Administrator";
        Common.Logger.Information($"Account {accountId} Crowns set to {request.Crowns} by {caller}");

        return Ok(new { message = "Crowns updated.", crowns = request.Crowns });
    }

    /// <summary>
    /// Updates an account's AuthLevel (Administrator only).
    /// </summary>
    [HttpPut("{accountId}/auth-level")]
    [Authorize(Policy = ApiScopes.AccountsWrite)]
    public IActionResult SetAuthLevel(ulong accountId, [FromBody] SetAuthLevelRequest request) {
        if (!Enum.TryParse<AuthLevel>(request.AuthLevel, ignoreCase: true, out var newLevel)) {
            return BadRequest(new { error = "Invalid AuthLevel." });
        }

        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null) {
            return NotFound(new { error = "Account not found." });
        }

        if (!AccountCollection.UpdateAuthLevel(account.Username, newLevel)) {
            return StatusCode(500, new { error = "Failed to update AuthLevel." });
        }

        var caller = User.Identity?.Name ?? "Administrator";
        Common.Logger.Information($"Account '{account.Username}' AuthLevel changed to {newLevel} by {caller}");

        return Ok(new { message = "AuthLevel updated.", authLevel = newLevel.ToString() });
    }

    /// <summary>
    /// Teleports an online player to the target zone via ZoneService, or updates their saved zone if offline.
    /// </summary>
    [HttpPost("/players/{charId}/teleport")]
    [Authorize(Policy = ApiScopes.CharactersWrite)]
    public IActionResult TeleportPlayer(ulong charId, [FromBody] TeleportRequest request) {
        var zoneName = request.ZoneName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(zoneName)) {
            return BadRequest(new { error = "ZoneName is required." });
        }

        var onlinePlayer = OnlinePlayerCollection.GetOnlinePlayer(charId);
        if (onlinePlayer is not null && !string.IsNullOrWhiteSpace(onlinePlayer.ActorPath)) {
            actorSystem.ActorSelection(onlinePlayer.ActorPath).Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
                DestinationZone = zoneName,
                DestinationLocation = request.Location?.Trim() ?? string.Empty,
                SendToClient = true
            });

            var caller = User.Identity?.Name ?? "Staff";
            Common.Logger.Information($"Online character {charId} teleported to '{zoneName}' by {caller}");
            return Ok(new { message = $"Teleporting online player to {zoneName}." });
        }

        // Player is offline: update their saved zone directly in RavenDB
        var wizard = WizardCollection.GetCharacter(charId);
        if (wizard is null) {
            return NotFound(new { error = "Character not found." });
        }

        WizardCollection.UpdateCharacterZone(wizard, zoneName, zoneName);
        return Ok(new { message = $"Updated offline character's zone to {zoneName}." });
    }

    /// <summary>
    /// Helper that finds an account's active online session (if any) and sends "Close" to its SessionActor.
    /// </summary>
    private bool KickOnlineAccountIfConnected(ulong accountId) {
        var onlinePlayer = OnlinePlayerCollection.GetOnlinePlayers()
            .FirstOrDefault(p => p.AccountId == accountId);

        if (onlinePlayer is null || string.IsNullOrWhiteSpace(onlinePlayer.ActorPath)) {
            return false;
        }

        actorSystem.ActorSelection(onlinePlayer.ActorPath).Tell("Close");
        OnlinePlayerCollection.RemoveOnlinePlayer(onlinePlayer.SessionId);

        return true;
    }
}
