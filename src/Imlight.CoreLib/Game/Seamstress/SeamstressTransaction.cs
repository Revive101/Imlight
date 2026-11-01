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
 * SEAMSTRESS TRANSACTION
 * ========================================================================
 *
 * PURPOSE:
 * Swaps the inputs of a stitch or an unstitch for their results and charges the Crowns as one unit: the
 * player ends up with either everything that was agreed or nothing of it.
 *
 * USAGE EXAMPLE:
 * var transaction = new SeamstressTransaction(wizard, session, unequip);
 * if (!transaction.TryApply([displayItem, statsItem], [stitchedItem], stitchCrowns, out var failure)) { deny(failure); }
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Seamstress;

/// <summary>
/// One stitch or unstitch for one wizard. See the header for the order of its steps.
/// </summary>
/// <param name="wizard">The wizard that stitches.</param>
/// <param name="session">The SessionActor reference of the wizard's session.</param>
/// <param name="unequip">
/// Takes an equipped item off the wizard and returns whether it is no longer equipped.
/// </param>
internal sealed class SeamstressTransaction(Wizard wizard, IActorRef session, Func<ulong, bool> unequip) {

    /// <summary>
    /// Replaces the inputs with the results and charges the Crowns.
    /// </summary>
    /// <param name="inputs">The items the player gives up. They may be in the backpack or equipped.</param>
    /// <param name="results">The new items. They are not stored yet.</param>
    /// <param name="crownsCost">The Crowns to charge. Zero charges nothing.</param>
    /// <param name="failure">Why nothing happened. Empty on success.</param>
    /// <returns>True when the player paid for the results and holds them. False when nothing changed for good.</returns>
    internal bool TryApply(IReadOnlyList<WizClientObjectItem> inputs, IReadOnlyList<WizClientObjectItem> results,
                           int crownsCost, out string failure) {
        var account = wizard.Account;
        var equippedInputs = inputs.Where(input => wizard.EquipmentBehavior.HasItemEquipped(input.m_globalID)).ToList();

        // An equipped input takes a backpack slot when it comes off, and the results are added before the inputs go.
        var slotsNeeded = results.Count + equippedInputs.Count;
        if (wizard.InventoryBehavior.FreeSlots < slotsNeeded) {
            failure = $"{slotsNeeded} free inventory slots are needed and {wizard.InventoryBehavior.FreeSlots} are free";

            return false;
        }

        if (account.Crowns < crownsCost) {
            failure = $"{crownsCost} Crowns are needed and the account has {account.Crowns}";

            return false;
        }

        if (!TryPrepareResults(results, out failure) || !TryUnequip(equippedInputs, out failure)) {
            return false;
        }

        var crownsBefore = account.Crowns;
        var added = new List<WizClientObjectItem>();
        var removed = new List<WizClientObjectItem>();
        try {
            if (!TryChangeBackpack(inputs, results, added, removed, out failure)) {
                PutBack(crownsBefore, added, removed);

                return false;
            }

            account.Crowns = crownsBefore - crownsCost;
            if (!AccountCollection.SaveCrownsWithBackpack(account, wizard, results, [.. inputs.Select(input => (ulong) input.m_globalID)])) {
                failure = "the new items and the Crowns could not be stored";
                PutBack(crownsBefore, added, removed);

                return false;
            }
        } catch (Exception ex) {
            Logger.Error("[Seamstress] Error while storing a seamstress request of {CharId}: {Reason} {StackTrace}",
                Logger.Args(wizard.CharId, ex.Message, ex.StackTrace));
            failure = "an error occurred while storing the new items and the Crowns";
            PutBack(crownsBefore, added, removed);

            return false;
        }

        TellClient(inputs, results, crownsCost);
        failure = string.Empty;

        return true;
    }

    private bool TryPrepareResults(IReadOnlyList<WizClientObjectItem> results, out string failure) {
        // The client is told about the results only after the commit, so a result it could not be told about stops here.
        foreach (var result in results) {
            CoreObjectFactory.InitializeCoreObjectBehaviors(result, result.m_templateID);
            result.m_characterId = wizard.CharId;
            if (!ItemSync.TrySerialize(result, out _)) {
                failure = $"item {result.m_globalID.Full} could not be serialized for the client";

                return false;
            }
        }

        failure = string.Empty;

        return true;
    }

    private bool TryUnequip(IReadOnlyList<WizClientObjectItem> equippedInputs, out string failure) {
        foreach (var input in equippedInputs) {
            if (!unequip(input.m_globalID)) {
                failure = $"item {input.m_globalID.Full} could not be unequipped";

                return false;
            }
        }

        failure = string.Empty;

        return true;
    }

    private bool TryChangeBackpack(IReadOnlyList<WizClientObjectItem> inputs, IReadOnlyList<WizClientObjectItem> results,
                                   List<WizClientObjectItem> added, List<WizClientObjectItem> removed, out string failure) {
        foreach (var result in results) {
            if (!wizard.InventoryBehavior.AddItem(result)) {
                failure = $"item {result.m_globalID.Full} could not be added to the inventory";

                return false;
            }

            added.Add(result);
        }

        foreach (var input in inputs) {
            if (!wizard.InventoryBehavior.RemoveItem(input)) {
                failure = $"item {input.m_globalID.Full} is not in the inventory";

                return false;
            }

            removed.Add(input);
        }

        failure = string.Empty;

        return true;
    }

    private void PutBack(int crownsBefore, IReadOnlyList<WizClientObjectItem> added, IReadOnlyList<WizClientObjectItem> removed) {
        wizard.Account.Crowns = crownsBefore;
        foreach (var item in added) {
            wizard.InventoryBehavior.RemoveItem(item);
        }

        foreach (var item in removed) {
            if (!wizard.InventoryBehavior.AddItem(item)) {
                Logger.Error("[Seamstress] Could not return item {ItemId} to the backpack of character {CharId} after a failed request.",
                    Logger.Args(item.m_globalID.Full, wizard.CharId));
            }
        }
    }

    private void TellClient(IReadOnlyList<WizClientObjectItem> inputs, IReadOnlyList<WizClientObjectItem> results, int crownsCost) {
        foreach (var result in results) {
            if (!ItemSync.SendAdded(session, wizard, result)) {
                Logger.Error("[Seamstress] Could not tell the client about item {ItemId} of character {CharId}, who relogs to see it.",
                    Logger.Args(result.m_globalID.Full, wizard.CharId));
            }
        }

        foreach (var input in inputs) {
            ItemSync.SendRemoved(session, wizard, input.m_globalID);
        }

        if (crownsCost > 0) {
            session.Tell(new WIZARD_12_PROTOCOL.MSG_CROWNBALANCE {
                Failure = 0,
                TotalCrowns = wizard.Account.Crowns,
                CharacterID = wizard.CharId,
                CacheBalanceForCSSegmentation = 1
            });
        }
    }

}
