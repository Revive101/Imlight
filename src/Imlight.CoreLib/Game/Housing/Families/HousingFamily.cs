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
 * The families of house objects. A family is a kind of object that needs more than a position in a house:
 * its own blob entry, its own state or its own server feature. The behaviors on an item's template pick its
 * family, and a family that is not enabled yet is declared all the same.
 *
 * USAGE EXAMPLE:
 * var families = HousingFamilies.Of(template);
 * if (families.All(family => family.Enabled)) {
 *     // the object may be placed
 * }
 *
 * NOTE:
 * - A trigger is an Imcodec type: a behavior template type, or the behavior type that the template's behavior
 *   name allocates, for the templates the data writes as a plain BehaviorTemplate.
 * - A template that carries no trigger is plain furniture. A template that carries triggers of several
 *   families belongs to all of them and is placed only when every one of them is enabled.
 * - AtticSubType is the blob subtype the client's BlobLoaded gives the attic blob of the family (0x142216a00).
 *   BlobSubType is the subtype of the family's live entries where BaseClientHousingBlobStrategy::CreateObject
 *   (0x1422170f0) names one; it is null where the client names none.
 * - Enabled says what the server supports yet, never what exists.
 *
 * TODO:
 * - Only the names tie InteractableBehavior to the Interact subtype and AquariumBehavior to FishAttic. Is that right?
 * - Which behavior puts an object in the Wand, JewelWand and HouseFish families?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Type = System.Type;

namespace Imlight.CoreLib.Game.Housing.Families;

/// <summary>
/// A kind of house object.
/// </summary>
/// <param name="Id">The family's name.</param>
/// <param name="BlobSubType">The subtype of its live blob entries, or <c>null</c> when the client names none.</param>
/// <param name="AtticSubType">The subtype of its attic blob, or <c>null</c> when the client has none for it.</param>
/// <param name="Triggers">The types that put a template in the family.</param>
/// <param name="Enabled">Whether the server supports the family yet.</param>
internal sealed record HousingFamily(
    string Id,
    string BlobSubType,
    string AtticSubType,
    IReadOnlyList<Type> Triggers,
    bool Enabled) {

    /// <summary>
    /// Checks whether any of the given types puts a template in this family.
    /// </summary>
    /// <param name="behaviorTypes">The types of one behavior: its template type and the type its name allocates.</param>
    /// <returns><c>true</c> when a trigger is one of the types or a base of one.</returns>
    internal bool IsTriggeredBy(IEnumerable<Type> behaviorTypes)
        => behaviorTypes.Any(type => Triggers.Any(trigger => trigger.IsAssignableFrom(type)));

}

/// <summary>
/// Every family of house objects the client knows.
/// </summary>
internal static class HousingFamilies {

    private static readonly ConcurrentDictionary<uint, IReadOnlyList<HousingFamily>> s_known = new();

    /// <summary>
    /// The family of an object that needs nothing beyond a position.
    /// </summary>
    internal static HousingFamily Plain { get; } = new("Plain", "Item", "Attic", [], true);

    /// <summary>
    /// The family of castle blocks, which the client's place and move messages can switch.
    /// </summary>
    internal static HousingFamily CastleBlock { get; } = Declare("CastleBlock", null, null,
        [typeof(CastleBlocksBehaviorTemplate), typeof(CastleBlockDoorBehaviorTemplate)]);

    /// <summary>
    /// The family of pets.
    /// </summary>
    internal static HousingFamily Pet { get; } = Declare("Pet", null, "PetAttic", [typeof(PetItemBehaviorTemplate)], enabled: true);

    /// <summary>
    /// The family of mounts.
    /// </summary>
    internal static HousingFamily Mount { get; } = Declare("Mount", null, "MountAttic",
        [typeof(RidableBehaviorTemplate), typeof(MountItemBehaviorTemplate)], enabled: true);

    /// <summary>
    /// Every declared family, plain furniture first.
    /// </summary>
    internal static IReadOnlyList<HousingFamily> All { get; } = [
        Plain,
        CastleBlock,
        Declare("ObjectState", null, null, [typeof(ObjectStateBehaviorTemplate)]),
        Pet,
        Mount,
        Declare("Interact", "Interact", "InteractAttic", [typeof(InteractableBehaviorTemplate)]),
        Declare("IntMusic", "IntMusic", "IntMusicAttic", [typeof(InteractiveMusicBehaviorTemplate)]),
        Declare("Music", null, "MusicAttic", [typeof(HousingMusicBehaviorTemplate), typeof(HousingMusicPlayerBehavior)]),
        Declare("Teleport", "Teleport", "TeleportAttic", [typeof(HousingTeleporterBehaviorTemplate)]),
        Declare("Sign", null, "SignAttic", [typeof(HousingSignBehaviorTemplate)]),
        Declare("Magic", "Magic", "MagicAttic", [typeof(CastleMagicBehaviorTemplate)]),
        Declare("Monster", "Monster", "MonsterAttic", [typeof(MonsterArenaBehaviorTemplate)]),
        Declare("Train", "Train", "TrainAttic", [typeof(TrainBehaviorTemplate)]),
        Declare("Equip", null, "EquipAttic", [typeof(EquipmentBehaviorTemplate)]),
        Declare("Fish", null, "FishAttic", [typeof(AquariumBehaviorTemplate)]),
        Declare("SeedShed", null, "SeedShedAttic", [typeof(GardeningShedBehaviorTemplate)]),
        Declare("BreadCrumb", null, "BreadCrumbAttic", [typeof(BreadCrumbBehaviorTemplate)]),
        Declare("Gear", null, "GearAttic", [typeof(GearVaultBehaviorTemplate)]),
        Declare("JewelVault", null, "JewelVaultAttic", [typeof(JewelVaultBehaviorTemplate)]),
        Declare("TCPoster", null, "TCPosterAttic", [typeof(TreasureCardPosterBehaviorTemplate)]),
        Declare("TCVault", null, "TCVaultAttic", [typeof(TreasureCardVaultBehaviorTemplate)]),
        Declare("Wand", null, "WandAttic", []),
        Declare("JewelWand", null, "JewelWandAttic", []),
        Declare("HouseFish", null, "HouseFishAttic", []),
        Declare("Sigil", null, null, [typeof(HousingSigilBehaviorTemplate)]),
        Declare("RaidKey", null, null, [typeof(RaidKeyBehaviorTemplate)]),
        Declare("ObjectRemap", null, null, [typeof(ObjectRemapBehaviorTemplate)]),
        Declare("ExtraZone", null, null, [typeof(ExtraHousingZoneBehaviorTemplate)]),
        Declare("Service", null, null, [
            typeof(BasicNPCServiceBehaviorTemplate), typeof(AlchemyStationBehaviorTemplate), typeof(WizShoppingBehaviorTemplate),
            typeof(WizardQuestingBehaviorTemplate), typeof(ItemFinderBehaviorTemplate), typeof(HousingHallOfFameBehaviorTemplate),
            typeof(HatchmakingKioskBehaviorTemplate), typeof(PvPKioskBehaviorTemplate), typeof(RitualObjectBehaviorTemplate)]),
    ];

    /// <summary>
    /// Finds the families a template belongs to.
    /// </summary>
    /// <param name="template">An item template.</param>
    /// <returns>The declared families whose triggers the template carries, or only <see cref="Plain"/> when it carries none.</returns>
    internal static IReadOnlyList<HousingFamily> Of(WizItemTemplate template)
        => s_known.GetOrAdd(template.m_templateID, _ => Resolve(template));

    private static HousingFamily Declare(
        string id, string blobSubType, string atticSubType, IReadOnlyList<Type> triggers, bool enabled = false)
        => new(id, blobSubType, atticSubType, triggers, enabled);

    private static IReadOnlyList<HousingFamily> Resolve(WizItemTemplate template) {
        var behaviorTypes = (template.m_behaviors ?? [])
            .Where(behavior => behavior is not null)
            .Select(BehaviorTypes)
            .ToList();
        var declared = All
            .Where(family => family != Plain && behaviorTypes.Any(family.IsTriggeredBy))
            .ToList();

        return declared.Count == 0 ? [Plain] : declared;
    }

    private static IEnumerable<Type> BehaviorTypes(BehaviorTemplate behavior) {
        yield return behavior.GetType();

        // Some templates are written as a plain BehaviorTemplate, and only the name says which behavior they mean.
        var instance = BehaviorCache.AllocateBehavior(StringHash.Compute(behavior.m_behaviorName));
        if (instance is not null) {
            yield return instance.GetType();
        }
    }

}
