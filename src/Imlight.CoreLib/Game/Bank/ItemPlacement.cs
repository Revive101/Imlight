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
 * ITEM PLACEMENT
 * ========================================================================
 *
 * PURPOSE:
 * Finds a place for a new item a player is given (a reward, an admin command): the backpack, and when that
 * is full the bank, and for a jewel the shared bank. An item with no room is lost. It also tells the client
 * where the item went.
 *
 * USAGE EXAMPLE:
 * var placement = ItemPlacement.Place(wizard, item);
 * ItemPlacement.Announce(session, wizard, item, placement);
 *
 * NOTE:
 * A jewel goes to the backpack until the wizard holds [Character] MaxJewelsAllowed of them, then to the shared
 * bank while it is under its jewel cap, then to the bank. The bank is only used while it is on, see
 * BankConfig.IsEnabled. A rental never goes into the bank. The client hears about an overflow item before the
 * loot popup, which shows it with the bank wording.
 *
 * TODO:
 * The server has no separate jewel pouch, jewels share the backpack slots. Is a full pouch the jewel count alone?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Bank;

internal static class ItemPlacement {

    private const string MAX_JEWELS_ALLOWED_KEY = "Character.MaxJewelsAllowed";

    /// <summary>
    /// Stores a new item where there is room for it.
    /// </summary>
    /// <param name="wizard">The wizard that is given the item.</param>
    /// <param name="item">The new item, with its behaviors initialized. It is stored nowhere yet.</param>
    /// <returns>Where the item is now. Lost means it was stored nowhere.</returns>
    internal static Placement Place(Wizard wizard, WizClientObjectItem item) {
        if (TryPlaceInBackpack(wizard, item)) {
            return Placement.Backpack;
        }

        if (BankRules.IsRental(item) || !BankConfig.IsEnabled) {
            return Placement.Lost;
        }

        // The shared bank is asked only when its stored state may agree, so a full bank does not log a refusal for every item.
        if (BankRules.IsJewel(item) && CanGoToSharedBank(wizard, item) && BankCollection.TryAddShared(wizard, item).Success) {
            return Placement.SharedBank;
        }

        if (wizard.StorageBehavior.HasRoomFor(item) && wizard.AddItemToBank(item)) {
            return Placement.Bank;
        }

        return Placement.Lost;
    }

    /// <summary>
    /// Tells the client where a new item went: the inventory add, the storage add with the overflow notice, or
    /// the notice that the item is gone.
    /// </summary>
    /// <param name="session">The SessionActor reference of the wizard's session.</param>
    /// <param name="wizard">The wizard that was given the item.</param>
    /// <param name="item">The item that was placed.</param>
    /// <param name="placement">What Place returned for it.</param>
    internal static void Announce(IActorRef session, Wizard wizard, WizClientObjectItem item, Placement placement) {
        switch (placement) {
            case Placement.Backpack:
                ItemSync.SendAdded(session, wizard, item);

                break;
            case Placement.Bank:
                SendOverflow(session, wizard, item, shared: false, wizard.StorageBehavior.Items.Count);

                break;
            case Placement.SharedBank:
                SendOverflow(session, wizard, item, shared: true, wizard.Account.SharedBank.Items.Count);

                break;
            default:
                Logger.Warning("There was no room for item {ItemId} (template {TemplateId}) of {CharId}, so it is lost.",
                    Logger.Args(item.m_globalID.Full, item.m_templateID.Full, wizard.CharId));
                session.Tell(BankSync.ItemDrop(item.m_templateID.Full));

                break;
        }
    }

    private static void SendOverflow(IActorRef session, Wizard wizard, WizClientObjectItem item, bool shared, int countInBank) {
        var add = BankSync.StorageAdd(wizard, item, shared);
        if (add is null) {
            return;
        }

        session.Tell(add);
        session.Tell(BankSync.ItemOverflowToBank(item.m_globalID, countInBank));
    }

    private static bool TryPlaceInBackpack(Wizard wizard, WizClientObjectItem item) {
        if (wizard.InventoryBehavior.FreeSlots < 1) {
            return false;
        }

        if (BankRules.IsJewel(item) && IsJewelPouchFull(wizard)) {
            return false;
        }

        return wizard.AddInitializedItemToInventory(item);
    }

    private static bool CanGoToSharedBank(Wizard wizard, WizClientObjectItem item)
        => BankRules.CanMove(BankContainer.Backpack, BankContainer.SharedBank, item, 1, wizard, wizard.Account) == BankDenial.None;

    private static bool IsJewelPouchFull(Wizard wizard) {
        var allowed = ConfigurationManager.GetValue(MAX_JEWELS_ALLOWED_KEY, 0);

        return allowed > 0 && wizard.InventoryBehavior.Items.Count(BankRules.IsJewel) >= allowed;
    }

}
