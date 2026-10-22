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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Picks the client blob a placed object lives in, and decides from an item's own template whether it may be placed in a
 * house. The client names live blobs by kind: plain objects go in "Item", and a kind that carries state (a pet, a mount)
 * goes in a blob of its own name, whose entries carry that state as a string. An add patch keeps the string only in a
 * kind-named blob. The client never names the kind in a request, so the server decides it from the template, the way the
 * client recognises it. Also counts copies of one item and takes the item of an object that leaves a house or an attic.
 *
 * USAGE EXAMPLE:
 * var verdict = HousingItems.Check(templateId, out var template);
 * switch (verdict.Placement) {
 *     case HousingPlacement.Allowed: // place it in the blob verdict.SubType
 *     case HousingPlacement.NotEnabled: // the client offers it, the server cannot serve its behavior yet
 *     default: // the client never offers this item
 * }
 *
 * NOTE:
 * - The placement rule is the client's: ObjectPlacementWindow::AddPlaceableItem 0x141062200 offers an item template that
 *   lacks the not-placeable adjective, whose object type is an object or a housing texture, or that has a pet or a mount
 *   behavior, and it skips seeds and rental mounts. A pet or a mount is found by its behavior, as the client finds it
 *   (GameObjectTemplate::FindPetItemBehaviorTemplate 0x14047e300, FindMountItemBehaviorTemplate 0x1404222f0).
 * - Kind names: BaseClientBlobPatcher::MSG_PatchAddHousingObject 0x14220d580. A pet's or mount's look comes from the entry
 *   string, the serialized item behavior: ClientHousingBlobStrategy::LoadMountOrPetBlobObject 0x140f2faf0.
 * - The attic needs one blob: the client loads every attic subtype the same way and decides custom entries by their string
 *   (ClientHousingBlobStrategy::LoadAtticBlobObjects 0x140f301c0, ClientBlobPatcher::MSG_PatchAddAttic 0x140f16c50).
 * - A kind is declared only when the server builds its state string. Until then, objects with a behavior the server cannot
 *   serve are refused without blame, because the client offers them (s_unsupportedBehaviors).
 * - Housing textures are offered by the client, and go through their own messages, so they are refused the same way.
 * - A placed or stored object keeps its item document, so it comes back as the same item. An object that has no document, as
 *   one a layout placed, or whose document is gone, comes back as a new item of its template.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Housing.Items;

/// <summary>
/// What the server does with a request to place an item.
/// </summary>
internal enum HousingPlacement {
    Allowed,
    NotEnabled,
    Refused,
}

/// <summary>
/// What the server decided about an item template.
/// </summary>
/// <param name="Placement">Whether the item may be placed.</param>
/// <param name="SubType">The subtype of the live blob the item's object is in: the kind of the item, whatever the placement.</param>
internal readonly record struct HousingVerdict(HousingPlacement Placement, string SubType);

internal static class HousingItems {

    /// <summary>
    /// The live blob of plain objects.
    /// </summary>
    internal const string ITEM_SUB_TYPE = "Item";

    /// <summary>
    /// The live blob of pets, whose entries carry the pet's look.
    /// </summary>
    internal const string PET_SUB_TYPE = "Pet";

    /// <summary>
    /// The live blob of mounts, whose entries carry the mount's look.
    /// </summary>
    internal const string MOUNT_SUB_TYPE = "Mount";

    /// <summary>
    /// The one blob of an attic, whatever the kind of its items.
    /// </summary>
    internal const string ATTIC_SUB_TYPE = "Attic";

    private const string NOT_PLACEABLE_ADJECTIVE = "NotPlaceable";
    private const uint PET_PROPERTY_MASK = 1u << 7;
    private const uint MOUNT_PROPERTY_MASK = 1u << 16;

    private static readonly string[] s_liveSubTypes = [ITEM_SUB_TYPE, PET_SUB_TYPE, MOUNT_SUB_TYPE];
    private static readonly StripedLocks s_backpackLocks = new();

    // Behaviors of server features that are not built yet. The client offers such an item for placing, so the server refuses
    // it without blame. Delete a line when the feature lands.
    private static readonly Type[] s_unsupportedBehaviors = [
        typeof(CastleBlocksBehaviorTemplate),
        typeof(CastleBlockDoorBehaviorTemplate),
        typeof(ObjectStateBehaviorTemplate),
        typeof(InteractableBehaviorTemplate),
        typeof(InteractiveMusicBehaviorTemplate),
        typeof(HousingMusicBehaviorTemplate),
        typeof(HousingTeleporterBehaviorTemplate),
        typeof(HousingSignBehaviorTemplate),
        typeof(CastleMagicBehaviorTemplate),
        typeof(MonsterArenaBehaviorTemplate),
        typeof(TrainBehaviorTemplate),
        typeof(EquipmentBehaviorTemplate),
        typeof(AquariumBehaviorTemplate),
        typeof(GardeningShedBehaviorTemplate),
        typeof(BreadCrumbBehaviorTemplate),
        typeof(GearVaultBehaviorTemplate),
        typeof(JewelVaultBehaviorTemplate),
        typeof(TreasureCardPosterBehaviorTemplate),
        typeof(TreasureCardVaultBehaviorTemplate),
        typeof(HousingSigilBehaviorTemplate),
        typeof(RaidKeyBehaviorTemplate),
        typeof(ObjectRemapBehaviorTemplate),
        typeof(ExtraHousingZoneBehaviorTemplate),
        typeof(BasicNPCServiceBehaviorTemplate),
        typeof(AlchemyStationBehaviorTemplate),
        typeof(WizShoppingBehaviorTemplate),
        typeof(WizardQuestingBehaviorTemplate),
        typeof(ItemFinderBehaviorTemplate),
        typeof(HousingHallOfFameBehaviorTemplate),
        typeof(HatchmakingKioskBehaviorTemplate),
        typeof(PvPKioskBehaviorTemplate),
        typeof(RitualObjectBehaviorTemplate),
    ];

    /// <summary>
    /// The subtypes of the live blobs of a house.
    /// </summary>
    internal static IReadOnlyList<string> LiveSubTypes => s_liveSubTypes;

    /// <summary>
    /// Gets the lock that keeps one character's backpack list from changing in two places at once while house items are
    /// issued, placed, picked up and moved to or from the attic.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <returns>The lock to hold for as long as the backpack is read and then changed.</returns>
    internal static Lock BackpackLock(Wizard wizard) => s_backpackLocks.For(wizard.CharId);

    /// <summary>
    /// Decides whether the server places an item template, and in which live blob its object goes.
    /// </summary>
    /// <param name="template">An item template.</param>
    /// <returns>
    /// <see cref="HousingPlacement.Refused"/> for an item the client never offers, <see cref="HousingPlacement.NotEnabled"/>
    /// for an item it offers but the server cannot serve yet, otherwise <see cref="HousingPlacement.Allowed"/>.
    /// </returns>
    internal static HousingVerdict Classify(WizItemTemplate template) {
        if (template is null) {
            return new HousingVerdict(HousingPlacement.Refused, ITEM_SUB_TYPE);
        }

        var subType = SubTypeOf(template);
        var behaviors = (template.m_behaviors ?? []).Where(behavior => behavior is not null).ToList();
        var adjectives = (template.m_adjectiveList ?? []).Select(adjective => adjective.ToString());
        if (adjectives.Contains(NOT_PLACEABLE_ADJECTIVE, StringComparer.Ordinal)
            || behaviors.OfType<SeedBehaviorTemplate>().Any()
            || (subType == MOUNT_SUB_TYPE && behaviors.OfType<RentalBehaviorTemplate>().Any())) {
            return new HousingVerdict(HousingPlacement.Refused, subType);
        }

        var isObject = template.m_nObjectType is ObjectType.OT_OBJECT or ObjectType.OT_HOUSING_TEXTURE;
        if (!isObject && subType == ITEM_SUB_TYPE) {
            return new HousingVerdict(HousingPlacement.Refused, subType);
        }

        var supported = template.m_nObjectType != ObjectType.OT_HOUSING_TEXTURE
            && !behaviors.Any(behavior => s_unsupportedBehaviors.Any(type => type.IsInstanceOfType(behavior)));

        return new HousingVerdict(supported ? HousingPlacement.Allowed : HousingPlacement.NotEnabled, subType);
    }

    /// <summary>
    /// Finds the template of an item and decides whether the server places it.
    /// </summary>
    /// <param name="templateId">The item's template id.</param>
    /// <param name="template">The item template when it exists and is an item.</param>
    /// <returns>The verdict; <see cref="HousingPlacement.Refused"/> when the template is not an item.</returns>
    internal static HousingVerdict Check(uint templateId, out WizItemTemplate template) {
        template = CoreObjectFactory.GetCoreTemplate(templateId) as WizItemTemplate;

        return Classify(template);
    }

    /// <summary>
    /// Finds the live blob an item template's objects are in: the pet or mount blob by the client's own behavior test,
    /// otherwise the plain one.
    /// </summary>
    /// <param name="template">An item template.</param>
    /// <returns>The blob's subtype.</returns>
    internal static string SubTypeOf(WizItemTemplate template) {
        var behaviors = (template?.m_behaviors ?? []).Where(behavior => behavior is not null).ToList();
        if (behaviors.OfType<PetItemBehaviorTemplate>().Any()) {
            return PET_SUB_TYPE;
        }

        return behaviors.OfType<MountItemBehaviorTemplate>().Any() ? MOUNT_SUB_TYPE : ITEM_SUB_TYPE;
    }

    /// <summary>
    /// Builds what an entry of a kind blob carries: the item's own behavior, serialized the way the client reads it.
    /// </summary>
    /// <param name="subType">The subtype of the live blob the object is in.</param>
    /// <param name="item">The item document, or <c>null</c> when it is gone; a new behavior stands in for it.</param>
    /// <param name="itemGid">The global id of the item.</param>
    /// <returns>The serialized behavior, or <c>null</c> for a plain object or when it cannot be serialized.</returns>
    internal static byte[] CustomDataOf(string subType, WizClientObjectItem item, ulong itemGid) {
        try {
            return subType switch {
                PET_SUB_TYPE => (byte[]) HousingBlobs.Serialize(PetBehaviorOf(item), PET_PROPERTY_MASK),
                MOUNT_SUB_TYPE => (byte[]) HousingBlobs.Serialize(MountBehaviorOf(item, itemGid), MOUNT_PROPERTY_MASK),
                _ => null,
            };
        } catch (InvalidOperationException exception) {
            Logger.Warning("Could not serialize the {SubType} behavior of item {ItemId}: {Reason}", Logger.Args(subType, itemGid, exception.Message));

            return null;
        }
    }

    /// <summary>
    /// Loads the item document of an object and builds its entry data, for an object that is stored without it.
    /// </summary>
    /// <param name="charId">The character who owns the item.</param>
    /// <param name="subType">The subtype of the live blob the object is in.</param>
    /// <param name="itemGid">The item's global id, or 0 for an object that has no document.</param>
    /// <returns>The serialized behavior, or <c>null</c> for a plain object.</returns>
    internal static byte[] LoadCustomData(ulong charId, string subType, ulong itemGid) {
        if (subType == ITEM_SUB_TYPE) {
            return null;
        }

        WizClientObjectItem item = null;
        try {
            item = itemGid == 0 ? null : WizardItemCollection.Load(charId, itemGid);
        } catch (Exception exception) {
            Logger.Warning("Could not load the stored item {ItemId} of character {CharId}: {Reason}",
                Logger.Args(itemGid, charId, exception.Message));
        }

        return CustomDataOf(subType, item, itemGid);
    }

    /// <summary>
    /// Checks whether a template may not have another copy: the client's limit on how often a house may hold
    /// one item, and how many copies a backpack may hold of it.
    /// </summary>
    /// <param name="template">The item template.</param>
    /// <param name="copies">The copies already held.</param>
    /// <returns><c>true</c> when the template has a limit and <paramref name="copies"/> reaches it.</returns>
    internal static bool TemplateCopyLimitReached(WizItemTemplate template, int copies)
        => template.m_itemLimit > 0 && copies >= template.m_itemLimit;

    /// <summary>
    /// Counts the copies of a template in a backpack.
    /// </summary>
    /// <param name="wizard">The character.</param>
    /// <param name="templateId">The item template.</param>
    /// <returns>The number of items of the template.</returns>
    internal static int CountInBackpack(Wizard wizard, uint templateId)
        => wizard.InventoryBehavior.Items.Count(item => item.m_templateID == templateId);

    /// <summary>
    /// Creates the backpack item for an object that is picked up. The item is not added to the backpack.
    /// </summary>
    /// <param name="wizard">The character who picks the object up.</param>
    /// <param name="templateId">The object's template id.</param>
    /// <returns>The new item, or <c>null</c> when the template is not an item.</returns>
    internal static WizClientObjectItem CreateItem(Wizard wizard, uint templateId) {
        if (CoreObjectFactory.FinalizeCoreObject(templateId) is not WizClientObjectItem item) {
            return null;
        }

        item.m_characterId = (GID) wizard.CharId;
        CoreObjectFactory.InitializeCoreObjectBehaviors(item, item.m_templateID);

        return item;
    }

    /// <summary>
    /// Takes the item of an object that leaves a house or an attic. The item is not added to the backpack.
    /// </summary>
    /// <param name="wizard">The character who takes the object.</param>
    /// <param name="templateId">The object's template id.</param>
    /// <param name="itemGid">The item document the object was placed from, or 0 for an object that has none.</param>
    /// <param name="created">
    /// <c>true</c> when the item is new and its document must be stored with the change that moves it; <c>false</c> when the
    /// stored document of the object is used.
    /// </param>
    /// <returns>The item, or <c>null</c> when the document could not be read or the template is not an item.</returns>
    internal static WizClientObjectItem TakeItem(Wizard wizard, uint templateId, ulong itemGid, out bool created) {
        created = false;
        if (itemGid != 0) {
            try {
                if (WizardItemCollection.Load(wizard.CharId, itemGid) is { } stored) {
                    return stored;
                }
            } catch (Exception exception) {
                Logger.Warning("Could not load the stored item {ItemId} of character {CharId}: {Reason}",
                    Logger.Args(itemGid, wizard.CharId, exception.Message));

                return null;
            }

            Logger.Warning("Item {ItemId} of character {CharId} has no stored document, so template {TemplateId} comes back as a new item.",
                Logger.Args(itemGid, wizard.CharId, templateId));
        }

        created = true;

        return CreateItem(wizard, templateId);
    }

    private static ClientPetItemBehavior PetBehaviorOf(WizClientObjectItem item)
        => item is not null && CoreObjectFactory.FindBehaviorInstance<ClientPetItemBehavior>(item, out var pet) ? pet : new ClientPetItemBehavior();

    private static MountItemBehavior MountBehaviorOf(WizClientObjectItem item, ulong itemGid)
        => item is not null && CoreObjectFactory.FindBehaviorInstance<MountItemBehavior>(item, out var mount)
            ? mount
            : new MountItemBehavior { m_housingMountOriginalGID = (GID) itemGid };

}
