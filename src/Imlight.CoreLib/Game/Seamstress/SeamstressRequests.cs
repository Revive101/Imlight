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
 * SEAMSTRESS REQUESTS
 * ========================================================================
 *
 * PURPOSE:
 * Does what the stitch and unstitch messages ask of the server: checks the seamstress NPC and the range, finds
 * the items, plans and builds the results, then lets a SeamstressTransaction swap the items and charge the Crowns.
 *
 * USAGE EXAMPLE:
 * var requests = new SeamstressRequests(wizard, SessionActor.ActorRef, GetZoneObject, GetActiveGameObject, UnequipItem);
 * var success = requests.Stitch(message);
 *
 * NOTE:
 * The client sends item ids only. It never checks for a free backpack slot and never takes off a worn
 * input, so the transaction does both. A refusal shows the client only its generic failure box.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Seamstress;

/// <summary>
/// The stitch and unstitch requests of one wizard.
/// </summary>
/// <param name="wizard">The wizard that asks.</param>
/// <param name="session">The SessionActor reference of the wizard's session.</param>
/// <param name="findZoneObject">Finds an entity of the wizard's zone by its global id, or null if there is none.</param>
/// <param name="getWizardObject">Gets the game object of the wizard.</param>
/// <param name="unequip">Takes an equipped item off the wizard and returns whether it is no longer equipped.</param>
internal sealed class SeamstressRequests(Wizard wizard, IActorRef session, Func<ulong, ZoneEntity> findZoneObject,
                                         Func<CoreObject> getWizardObject, Func<ulong, bool> unequip) {

    /// <summary>
    /// Stitches two items into one: the stats of one with the look of the other.
    /// </summary>
    /// <param name="message">The client's request. GlobalID names the seamstress NPC.</param>
    /// <returns>True when the wizard paid and holds the stitched item. False when nothing changed.</returns>
    internal bool Stitch(WIZARD_12_PROTOCOL.MSG_STITCHITEMS message)
        => SeamstressConfig.TryGetPrices(out var stitchCrowns, out _)
        && IsAtSeamstress(message.GlobalID)
        && TryStitch(message.DisplayID, message.StatsID, stitchCrowns);

    /// <summary>
    /// Splits a stitched item into the item with its stats and the item with its look.
    /// </summary>
    /// <param name="message">The client's request. GlobalID names the seamstress NPC.</param>
    /// <returns>True when the wizard paid and holds both items. False when nothing changed.</returns>
    internal bool Unstitch(WIZARD_12_PROTOCOL.MSG_UNSTITCHITEMS message)
        => SeamstressConfig.TryGetPrices(out _, out var unstitchCrowns)
        && IsAtSeamstress(message.GlobalID)
        && TryUnstitch(message.ItemID, ClampToInt(message.StatItemColor1), ClampToInt(message.StatItemColor2), unstitchCrowns);

    private bool TryStitch(ulong displayItemId, ulong statsItemId, int stitchCrowns) {
        var displayItem = FindItem(displayItemId);
        var statsItem = FindItem(statsItemId);
        if (displayItemId == statsItemId || displayItem is null || statsItem is null) {
            ReportTampering($"Player tried to stitch items {displayItemId} and {statsItemId}, which are not two items they own!");

            return false;
        }

        if (!StitchRules.TryPlanStitch(displayItem, statsItem, out var plan, out var reason)) {
            return Deny($"stitch of items {displayItemId} and {statsItemId}", reason);
        }

        if (!StitchedItemFactory.TryCreateStitched(wizard, plan, out var stitchedItem)) {
            return Deny($"stitch of items {displayItemId} and {statsItemId}", "the result could not be built");
        }

        ReportJewelSockets(displayItem, statsItem);
        if (!TryApply([displayItem, statsItem], [stitchedItem], stitchCrowns, out var failure)) {
            return Deny($"stitch of items {displayItemId} and {statsItemId}", failure);
        }

        return true;
    }

    private bool TryUnstitch(ulong itemId, int statItemColor1, int statItemColor2, int unstitchCrowns) {
        var stitchedItem = FindItem(itemId);
        if (stitchedItem is null) {
            ReportTampering($"Player tried to unstitch item {itemId}, which they do not own!");

            return false;
        }

        if (!StitchRules.TryPlanUnstitch(stitchedItem, statItemColor1, statItemColor2, out var plan, out var reason)) {
            return Deny($"unstitch of item {itemId}", reason);
        }

        if (!StitchedItemFactory.TryCreate(wizard, plan.Stats, out var statsItem)
            || !StitchedItemFactory.TryCreate(wizard, plan.Appearance, out var appearanceItem)) {
            return Deny($"unstitch of item {itemId}", "the results could not be built");
        }

        ReportJewelSockets(stitchedItem);
        if (!TryApply([stitchedItem], [statsItem, appearanceItem], unstitchCrowns, out var failure)) {
            return Deny($"unstitch of item {itemId}", failure);
        }

        return true;
    }

    private bool TryApply(WizClientObjectItem[] inputs, WizClientObjectItem[] results, int crownsCost, out string failure) {
        try {
            var transaction = new SeamstressTransaction(wizard, session, unequip);

            return transaction.TryApply(inputs, results, crownsCost, out failure);
        } catch (Exception ex) {
            Logger.Error("[Seamstress] Error in a request of {CharId}: {Reason} {StackTrace}", Logger.Args(wizard.CharId, ex.Message, ex.StackTrace));
            failure = "an error occurred";

            return false;
        }
    }

    private bool Deny(string request, string reason) {
        Logger.Warning("[Seamstress] Refused the {Request} for {CharId}: {Reason}", Logger.Args(request, wizard.CharId, reason));

        return false;
    }

    private bool IsAtSeamstress(ulong npcGlobalId) {
        var npc = findZoneObject(npcGlobalId);
        if (npc is null) {
            Logger.Warning("[Seamstress] Failed to find NPC {NpcId} in the zone of {CharId}", Logger.Args(npcGlobalId, wizard.CharId));

            return false;
        }

        var seamstress = npc.GetComponentOfType<InteractSeamstressComponent>();
        if (seamstress is null) {
            ReportTampering($"Player sent a seamstress request to NPC {npcGlobalId}, which is not a seamstress!");

            return false;
        }

        if (!seamstress.IsInServiceRange(getWizardObject())) {
            ReportTampering($"Player sent a seamstress request to NPC {npcGlobalId} from out of range!");

            return false;
        }

        return true;
    }

    private void ReportTampering(string infractionText) {
        wizard.Account.AddInfraction(InfractionType.SuspiciousBehavior, infractionText);

        Logger.Warning("[Seamstress] {Infraction} This has been logged as a suspicious behavior infraction.", Logger.Args(infractionText));
    }

    private void ReportJewelSockets(params WizClientObjectItem[] items) {
        foreach (var item in items) {
            if (StitchedItemFactory.HasJewelSockets(item)) {
                Logger.Warning("[Seamstress] Item {ItemId} of {CharId} has jewel sockets, which the seamstress does not handle.",
                    Logger.Args(item.m_globalID.Full, wizard.CharId));
            }
        }
    }

    private WizClientObjectItem FindItem(ulong itemId)
        => wizard.InventoryBehavior.GetItem(itemId) ?? wizard.EquipmentBehavior.GetItem(itemId);

    private static int ClampToInt(uint value) => (int) Math.Min(value, int.MaxValue);

}
