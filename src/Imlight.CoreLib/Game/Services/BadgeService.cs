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
 * BADGE SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Sends a character's badges to the client, keeps them current as the character earns them, and handles choosing and inspecting the displayed badge title.
 *
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory; MSG_PRELOGIN makes it send the full badge list, MSG_COMBATWIN advances the kill counters.
 *
 * NOTE:
 * The list is sent before AttachService answers its own MSG_PRELOGIN with MSG_LOGINCOMPLETE, as the live server does.
 * Every state change goes through BadgeEngine; this service persists it once, tells the client and runs the completion results.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Badges;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Services;

internal class BadgeService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const int MAX_LISTED_BADGES = 25;

    private static readonly uint s_selectBadgeError = StringHash.Compute("SelectBadgeError");
    private static readonly uint s_badgeDoesNotExist = StringHash.Compute("BadgeDoesNotExist");
    private static readonly uint s_badgeNotComplete = StringHash.Compute("BadgeNotComplete");

    private readonly HashSet<uint> _warnedUnknownBadges = [];

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new BadgeService(parentActor));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PRELOGIN))]
    private void ReceivePreLogin(ZONE_102_PROTOCOL.MSG_PRELOGIN message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return;
        }

        // An award that is due at login is already in the full list, so the changes are not sent on their own.
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var changes = Recheck(wizard, catalog);
        FinishChanges(wizard, changes, sendBatches: false);

        SendFullList(wizard, catalog);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BADGEEVENT))]
    private void ReceiveBadgeEvent(CHARACTER_103_PROTOCOL.MSG_BADGEEVENT message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var changes = message.Kind switch {
            BadgeEventKind.Award => new BadgeEngine(wizard, catalog).Execute(engine => engine.Award(message.BadgeId)),
            BadgeEventKind.Recheck => Recheck(wizard, catalog),
            _ => BadgeEngine.Changes.None
        };

        if (message.Kind == BadgeEventKind.Award) {
            WarnIfUnknown(wizard, catalog, message.BadgeId);
        }

        FinishChanges(wizard, changes, sendBatches: true);
    }

    [MessageHandler(typeof(GROUP_109_PROTOCOL.MSG_LEVELCHANGED))]
    private void ReceiveLevelChanged(GROUP_109_PROTOCOL.MSG_LEVELCHANGED message) {
        ReceiveBadgeEvent(new CHARACTER_103_PROTOCOL.MSG_BADGEEVENT { Kind = BadgeEventKind.Recheck });
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATWIN))]
    private void ReceiveCombatVictory(COMBAT_106_PROTOCOL.MSG_COMBATWIN message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady || message.MobTemplateIds is not { Length: > 0 }) {
            return;
        }

        var kills = CountKills(catalog, message.MobTemplateIds);
        if (kills.Count == 0) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var changes = new BadgeEngine(wizard, catalog).Execute(engine => {
            foreach (var (counterKey, count) in kills) {
                engine.Increment(counterKey, count);
            }
        });

        FinishChanges(wizard, changes, sendBatches: true);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_SELECT_BADGE))]
    private void ReceiveSelectBadge(GAME_5_PROTOCOL.MSG_SELECT_BADGE message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var state = wizard.BadgeBehavior;
        var requestedId = message.BadgeNameID == BadgeCatalog.s_noTitleId ? 0 : message.BadgeNameID;

        // The client re-sends its current badge when a PvP duel starts, so the current selection is a no-op.
        if (requestedId == state.SelectedBadgeId && TitleMatchesSelection(wizard, catalog)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_SELECT_BADGE { BadgeNameID = GetClientBadgeId(requestedId), Error = 0 });

            return;
        }

        BadgeDefinition chosen = null;
        var error = requestedId != 0 ? GetSelectionError(state, catalog, requestedId, out chosen) : 0;
        if (error != 0) {
            Logger.Warning("{0} could not select badge {1}, error {2}.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), requestedId, error));
            SendToSocket(new GAME_5_PROTOCOL.MSG_SELECT_BADGE { BadgeNameID = message.BadgeNameID, Error = error });

            return;
        }

        state.SelectedBadgeId = requestedId;
        Persist(wizard);
        wizard.SetBadgeOverride(chosen?.Title ?? string.Empty);

        SendToSocket(new GAME_5_PROTOCOL.MSG_SELECT_BADGE { BadgeNameID = GetClientBadgeId(requestedId), Error = 0 });

        BroadcastTitle(wizard);
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTPLAYERBADGE))]
    private void ReceiveRequestPlayerBadge(WIZARD3_56_PROTOCOL.MSG_REQUESTPLAYERBADGE message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return;
        }

        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        if (message.GlobalID != wizard.CharId) {
            Logger.Warning("{0} asked for a badge on behalf of character {1}.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.GlobalID));

            return;
        }

        var target = message.RequestedID == wizard.CharId ? wizard : WizardCollection.GetCharacterUnloaded(message.RequestedID);

        var displayed = GetDisplayedBadge(catalog, target);

        SendToSocket(new WIZARD3_56_PROTOCOL.MSG_REQUESTPLAYERBADGE {
            GlobalID = 0,
            RequestedID = message.RequestedID,
            BadgeName = displayed?.Title ?? string.Empty,
            BadgeDescription = displayed?.Info ?? string.Empty
        });
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND))]
    private void ReceiveBadgeCommand(CHARACTER_103_PROTOCOL.MSG_BADGECOMMAND message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var argument = message.Argument?.Trim() ?? string.Empty;
        if (message.Action == "title") {
            wizard.SetBadgeOverride(argument);
            BroadcastTitle(wizard);
            CommandReply($"Set the badge title to {argument}.");

            return;
        }

        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            CommandReply("The badge catalog is not loaded.");

            return;
        }

        switch (message.Action) {
            case "grant":
                CommandGrant(wizard, catalog, argument);
                break;
            case "progress":
                CommandProgress(wizard, catalog, argument, message.Amount);
                break;
            case "list":
                CommandList(wizard, catalog);
                break;
            case "reset":
                CommandReset(wizard, catalog);
                break;
            default:
                CommandReply($"Unknown badge action '{message.Action}'.");
                break;
        }
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTNEWBADGE))]
    private void ReceiveRequestNewBadge(WIZARD3_56_PROTOCOL.MSG_REQUESTNEWBADGE message) {
        var catalog = BadgeCatalog.Current;
        if (!catalog.IsReady) {
            return;
        }

        // The client sends this for each complete badge it has just been told about; no reply is expected.
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var state = wizard.BadgeBehavior;
        if (!catalog.TryGet(message.BadgeID, out var definition) || !BadgeView.IsComplete(state, definition)) {
            Logger.Warning("{0} acknowledged badge {1}, which they do not hold complete.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.BadgeID));

            return;
        }

        if (state.NewBadges.Any(entry => entry.BadgeId == message.BadgeID)) {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        state.AddNewBadge(new NewBadgeEntry { BadgeId = message.BadgeID, Time = (uint) now.ToUnixTimeSeconds() }, now);
        Persist(wizard);
    }

    private void FinishChanges(Wizard wizard, BadgeEngine.Changes changes, bool sendBatches) {
        if (changes.StateChanged) {
            Persist(wizard);
        }

        if (sendBatches) {
            foreach (var batch in changes.Batches) {
                foreach (var badgeMessage in BadgeWire.Build(wizard, BadgeCatalog.Current, batch, full: false)) {
                    SendToSocket(badgeMessage);
                }
            }
        }

        foreach (var completed in changes.Completed.Where(definition => definition.CompleteResults is not null)) {
            ResultDispatcher.ExecuteResults(
                actorContext: Context,
                results: completed.CompleteResults,
                playerRef: SessionActor.ActorRef,
                playerObj: GetActiveGameObject(),
                zoneActor: SessionActor.GetZoneActor());
        }
    }

    private void SendFullList(Wizard wizard, BadgeCatalog catalog) {
        foreach (var badgeMessage in BadgeWire.Build(wizard, catalog, BadgeView.GetHeld(wizard.BadgeBehavior, catalog), full: true)) {
            SendToSocket(badgeMessage);
        }
    }

    private void BroadcastTitle(Wizard wizard) {
        // The client rebuilds a name tag only on MSG_NEWTITLE or a new object, so the sender is included.
        ZoneBroadcast(new WIZARD_12_PROTOCOL.MSG_NEWTITLE {
            GlobalID = wizard.GameObjectID,
            PvPIconID = wizard.PlayerNameBehavior.PvpIconId,
            Title = wizard.PlayerNameBehavior.BadgeTitle
        }, isSelfless: false);
    }

    private void CommandReply(string text)
        => SendToSocket(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = text, Modal = 0 });

    private void CommandGrant(Wizard wizard, BadgeCatalog catalog, string idOrName) {
        if (!TryFindBadge(catalog, idOrName, out var definition)) {
            CommandReply($"No badge matches '{idOrName}'.");

            return;
        }

        var changes = new BadgeEngine(wizard, catalog).Execute(engine => engine.Award(definition.Id));
        FinishChanges(wizard, changes, sendBatches: true);

        var granted = changes.Completed.Count > 0;
        CommandReply(granted
            ? $"Granted {DescribeBadge(definition)}."
            : $"{DescribeBadge(definition)} was not granted: it is already earned or retired.");
    }

    private void CommandProgress(Wizard wizard, BadgeCatalog catalog, string counterKey, ulong amount) {
        if (catalog.GetFamily(counterKey).Count == 0) {
            CommandReply($"'{counterKey}' is not a badge counter key.");

            return;
        }

        var changes = new BadgeEngine(wizard, catalog).Execute(engine => engine.Increment(counterKey, amount));
        FinishChanges(wizard, changes, sendBatches: true);
        CommandReply($"Counter {counterKey} is now {wizard.BadgeBehavior.Counters.GetValueOrDefault(counterKey)}.");
    }

    private void CommandList(Wizard wizard, BadgeCatalog catalog) {
        var state = wizard.BadgeBehavior;
        var held = BadgeView.GetHeld(state, catalog).Where(d => d.Id != BadgeCatalog.s_noTitleId).ToList();
        if (held.Count == 0) {
            CommandReply("You hold no badges.");

            return;
        }

        foreach (var definition in held.Take(MAX_LISTED_BADGES)) {
            var progress = DescribeProgress(state, definition);
            CommandReply($"{DescribeBadge(definition)}: {definition.Description} [{progress}]");
        }

        if (held.Count > MAX_LISTED_BADGES) {
            CommandReply($"... and {held.Count - MAX_LISTED_BADGES} more.");
        }
    }

    private void CommandReset(Wizard wizard, BadgeCatalog catalog) {
        var hadTitle = wizard.BadgeBehavior.SelectedBadgeId != 0 || !wizard.PlayerNameBehavior.BadgeTitle.IsEmpty;
        wizard.BadgeBehavior.Reset();
        Persist(wizard);
        if (hadTitle) {
            wizard.SetBadgeOverride(string.Empty);
            BroadcastTitle(wizard);
        }

        SendFullList(wizard, catalog);
        CommandReply("Your badges were reset.");
    }

    private BadgeEngine.Changes Recheck(Wizard wizard, BadgeCatalog catalog)
        => new BadgeEngine(wizard, catalog).Execute(engine => engine.Recheck(definition => MeetsRequirements(wizard, definition)));

    private void WarnIfUnknown(Wizard wizard, BadgeCatalog catalog, uint badgeId) {
        if (catalog.TryGet(badgeId, out _) || !_warnedUnknownBadges.Add(badgeId)) {
            return;
        }

        Logger.Warning("Badge {0} is not in the badge catalog and was not awarded to {1}.",
            Logger.Args(badgeId, wizard.CharId));
    }

    private static Dictionary<string, ulong> CountKills(BadgeCatalog catalog, IEnumerable<ulong> mobTemplateIds) {
        // Each defeated mob advances a counter once, however many of its adjectives name that counter.
        var kills = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var templateId in mobTemplateIds) {
            if (CoreObjectFactory.GetCoreTemplate(templateId) is not GameObjectTemplate mob) {
                continue;
            }

            var adjectives = mob.m_adjectiveList?.Where(a => !a.IsEmpty).Select(a => (string) a) ?? [];
            foreach (var counterKey in catalog.GetCounterKeysForAdjectives(adjectives)) {
                kills[counterKey] = kills.GetValueOrDefault(counterKey) + 1;
            }
        }

        return kills;
    }

    private static BadgeDefinition GetDisplayedBadge(BadgeCatalog catalog, Wizard target) {
        // Nothing selected (or an unknown character) shows the "Display no badge title" description.
        var selectedId = target?.BadgeBehavior?.SelectedBadgeId ?? 0;
        if (catalog.TryGet(selectedId, out var selected)) {
            return selected;
        }

        catalog.TryGet(BadgeCatalog.s_noTitleId, out var noTitle);

        return noTitle;
    }

    private static bool TryFindBadge(BadgeCatalog catalog, string idOrName, out BadgeDefinition definition) {
        var isId = uint.TryParse(idOrName, NumberStyles.None, CultureInfo.InvariantCulture, out var id);

        return isId ? catalog.TryGet(id, out definition) : catalog.TryGetByName(idOrName, out definition);
    }

    private static string DescribeProgress(ServerBadgeBehavior state, BadgeDefinition definition)
        => BadgeView.IsComplete(state, definition) ? "complete" : $"{BadgeView.GetProgress(state, definition)}/{definition.Max}";

    private static string DescribeBadge(BadgeDefinition definition)
        => string.IsNullOrEmpty(definition.DisplayName) ? definition.Id.ToString(CultureInfo.InvariantCulture) : $"{definition.DisplayName} ({definition.Id})";

    private static uint GetClientBadgeId(uint selectedBadgeId)
        => selectedBadgeId != 0 ? selectedBadgeId : BadgeCatalog.s_noTitleId;

    private static bool TitleMatchesSelection(Wizard wizard, BadgeCatalog catalog) {
        // A title set by the mod badge command is not a selection, so choosing no title still clears it.
        var selectedId = wizard.BadgeBehavior.SelectedBadgeId;
        var expected = selectedId != 0 && catalog.TryGet(selectedId, out var selected) ? selected.Title : string.Empty;

        return string.Equals((string) wizard.PlayerNameBehavior.BadgeTitle, expected, StringComparison.Ordinal);
    }

    private static uint GetSelectionError(ServerBadgeBehavior state, BadgeCatalog catalog, uint badgeId, out BadgeDefinition definition) {
        // A badge the character never started is as unknown to them as one that is not in the catalog.
        if (!catalog.TryGet(badgeId, out definition) || !BadgeView.GetHeld(state, catalog).Contains(definition)) {
            return s_badgeDoesNotExist;
        }

        if (!BadgeView.IsComplete(state, definition)) {
            return s_badgeNotComplete;
        }

        return definition.ShowTitle ? 0 : s_selectBadgeError;
    }

    private static void Persist(Wizard wizard) {
        if (!WizardCollection.UpdateCharacterBadgeBehavior(wizard)) {
            Logger.Error("Failed to save the badge state of {0}.", Logger.Args(wizard.CharId));
        }
    }

    private bool MeetsRequirements(Wizard wizard, BadgeDefinition definition)
        => RequirementDispatcher.EvaluateRequirements(
            definition.Requirements,
            new GenericRequirementContext(definition.Requirements, SessionActor.ActorRef, GetActiveGameObject(), wizard, SessionActor.GetZoneActor()));

}
