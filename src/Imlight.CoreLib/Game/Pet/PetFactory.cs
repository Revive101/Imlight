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
 */

using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.Bit;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Pet;

public class PetFactory : RootDirectoryResourceSingleton<PetFactory>, IMemoryStreamDisposable {

    public const uint GENERIC_PET_TEMPLATE_ID = 2;
    private const float LeashRadius = 75f;
    private const int MaxSkinColor = 15;

    protected override string DirectoryName => "ObjectData/Pets/";

    private static readonly Dictionary<uint, GameObjectTemplate> s_petTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var count = 0;

        foreach (var (fileRecord, fileStream) in base.Files) {
            if (!serializer.Deserialize<GameObjectTemplate>(fileStream?.ToArray(), out var template)) {
                Logger.Error("Could not deserialize {0} as {1}",
                    Logger.Args(fileRecord.FileName, nameof(GameObjectTemplate)));

                continue;
            }

            var key = template.m_templateID;
            s_petTemplates[key] = template;
            count++;
        }

        Logger.Information("Loaded {0} pet templates.",
            Logger.Args(count));
    }

    public static bool IsPetTemplate(uint templateId) => s_petTemplates.ContainsKey(templateId);

    public static WizClientObjectItem CreateHatchedPet(ulong ownerId, uint templateId)
        => CreatePet(ownerId, templateId, preHatch: true);

    public static WizClientObjectItem CreatePet(
            ulong ownerId,
            uint templateId,
            bool preHatch = false
    ) {
        if (!s_petTemplates.TryGetValue(templateId, out var template)) {
            Logger.Error("Could not find pet template with ID {0}",
                Logger.Args(templateId));

            return null;
        }

        // Important behavior that can tell us race, egg name, hatch rate.
        var behaviorTemplate = template.m_behaviors
            .FirstOrDefault(s => s.m_behaviorName == "PetItemBehavior");
        if (behaviorTemplate == null || behaviorTemplate is not PetItemBehaviorTemplate petItemBehaviorTemplate) {
            Logger.Error("Pet template with ID {0} has no pet item behavior, or it is not of type {1}.",
                Logger.Args(templateId, nameof(PetItemBehaviorTemplate)));

            return null;
        }

        var parsedHatchRate = ParseHatchRate(petItemBehaviorTemplate.m_sHatchRate);
        var pet = new WizClientObjectItem {
            m_characterId = (GID) ownerId,
            m_globalID = RandomGen.GenerateGUID(),
            m_templateID = template.m_templateID,
            m_inactiveBehaviors = [
                new ClientPetItemBehavior() {
                    m_level = (byte) (preHatch ? 1 : 0),
                    m_XP = 0,
                    m_hatchedTimeSecs = (uint) (preHatch ? 0 : parsedHatchRate),
                },
                new ClientPetNameBehavior() {
                    m_eRace = petItemBehaviorTemplate.m_eRace,
                    m_eGender = petItemBehaviorTemplate.m_eGender,
                }
            ]
        };

        return pet;
    }

    /// <summary>
    /// Gives a pet item new packed name keys.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="nameKeys">The packed name keys the client renders the name from.</param>
    /// <returns>False if the item has no pet name behavior.</returns>
    public static bool TrySetPetName(WizClientObjectItem pet, uint nameKeys) {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var petName)) {
            return false;
        }

        petName.m_nameKeys = nameKeys;

        // The item behavior keeps the same parts unpacked; they are saved but never transmitted.
        if (CoreObjectFactory.FindBehaviorInstance<ClientPetItemBehavior>(pet, out var petItem)) {
            (petItem.m_firstName, petItem.m_middleName, petItem.m_lastName) = WizardNameBank.GetNameParts(nameKeys);
        }

        return true;
    }

    public static WizClientPet CreatePetGameObject(WizClientObjectItem pet, GID ownerId) {
        var genericPetObject = new WizClientPet();
        CoreObjectFactory.InitializeCoreObjectBehaviors(genericPetObject, GENERIC_PET_TEMPLATE_ID);

        // Live gives every summon its own world GID, never the pet item's.
        genericPetObject.m_globalID = RandomGen.GenerateGUID();
        genericPetObject.m_templateID = GENERIC_PET_TEMPLATE_ID;
        genericPetObject.m_leashed = true;

        // This single behavior on the pet can help us build both of the behaviors
        // on the game object.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var petNameBehaviorInstanceOnPet)) {
            Logger.Error("Pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(pet.m_globalID.Full, nameof(ClientPetNameBehavior)));

            return null;
        }

        // Replace the generic pet's behaviors with the pet's behaviors.
        genericPetObject = SetPetGameObjectBehaviors(genericPetObject, pet);
        if (genericPetObject is null) {
            return null;
        }

        // The client pairs behaviors with template slots by index, so the leash has to go
        // after all of the template's slots, null ones included.
        genericPetObject.m_inactiveBehaviors.Add(CreateLeash(ownerId));

        return genericPetObject;
    }

    private static LeashBehavior CreateLeash(GID ownerId) => new() {
        m_ownerGid = ownerId,
        m_radius = LeashRadius,
        m_angle = PickLeashAngle(),
        m_leashType = LeashType.LLT_Elastic,
        m_alwaysDisplay = false,
    };

    private static float PickLeashAngle()
        => Random.Shared.Next(2) == 0
            ? 90f + Random.Shared.NextSingle() * 30f
            : 210f + Random.Shared.NextSingle() * 60f;

    private static WizClientPet SetPetGameObjectBehaviors(WizClientPet petGameObject, WizClientObjectItem pet) {
        if (!CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var petNameBehaviorInstanceOnPet)) {
            Logger.Error("Pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(pet.m_globalID.Full, nameof(ClientPetNameBehavior)));

            return null;
        }

        // Replace the generic pet's behaviors with the pet's behaviors.
        if (CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(petGameObject, out var petNameBehaviorInstance)) {
            var idx = petGameObject.m_inactiveBehaviors.IndexOf(petNameBehaviorInstance);
            petGameObject.m_inactiveBehaviors[idx] = CreatePetNameBehavior(pet, petNameBehaviorInstanceOnPet);
        }
        else {
            Logger.Error("Generic pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(petGameObject.m_globalID.Full, nameof(ClientPetNameBehavior)));
        }

        // The race only picks the model; the texture and colors ride on this behavior.
        if (CoreObjectFactory.FindBehaviorInstance<WizardCharacterBehavior>(petGameObject, out var wizardCharacterBehaviorInstance)) {
            var idx = petGameObject.m_inactiveBehaviors.IndexOf(wizardCharacterBehaviorInstance);
            petGameObject.m_inactiveBehaviors[idx] = CreatePetAppearance(pet, petNameBehaviorInstanceOnPet);
        }
        else {
            Logger.Error("Generic pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(petGameObject.m_globalID.Full, nameof(WizardCharacterBehavior)));
        }

        return petGameObject;
    }

    private static ClientPetNameBehavior CreatePetNameBehavior(WizClientObjectItem pet, ClientPetNameBehavior itemName) {
        CoreObjectFactory.FindBehaviorInstance<ClientPetItemBehavior>(pet, out var petItemBehavior);

        return new ClientPetNameBehavior {
            m_nameKeys = itemName.m_nameKeys,
            m_useRank = true,
            m_eGender = itemName.m_eGender,
            m_eRace = itemName.m_eRace,
            m_overallRating = petItemBehavior?.m_overallRating ?? 0,
            m_activeRating = petItemBehavior?.m_activeRating ?? 0,
            m_petLevel = petItemBehavior?.m_level ?? 0,
            m_templateID = (uint) pet.m_templateID,
        };
    }

    private static WizardCharacterBehavior CreatePetAppearance(WizClientObjectItem pet, ClientPetNameBehavior itemName) {
        var petItemTemplate = GetPetItemBehaviorTemplate((uint) pet.m_templateID);
        if (petItemTemplate is null) {
            Logger.Warning("Pet {0} has no pet item behavior on template {1}; it will show its race's default look.",
                Logger.Args(pet.m_globalID.Full, pet.m_templateID.Full));
        }

        var primary = GetDyeTexture(petItemTemplate?.m_primaryDyeToTexture, pet.m_primaryColor);
        var secondary = GetDyeTexture(petItemTemplate?.m_secondaryDyeToTexture, pet.m_secondaryColor);
        var pattern = GetDyeTexture(petItemTemplate?.m_patternToTexture, pet.m_pattern);

        // Live writes the primary texture into every color slot and the secondary into every decal
        // slot; the 4-bit skin color saturates, the skin decal keeps its high bits in the extended field.
        return new WizardCharacterBehavior {
            m_eRace = itemName.m_eRace,
            m_eGender = itemName.m_eGender,
            m_nSkinColor = (Bui4) Math.Min(primary, MaxSkinColor),
            m_nHairColor = (Bui7) primary,
            m_nHatColor = (Bui5) primary,
            m_nTorsoColor = (Bui5) primary,
            m_nFeetColor = (Bui5) primary,
            m_nSkinDecal = (Bui4) (secondary & 0xF),
            m_extendedSkinDecal = (ushort) (secondary >> 4),
            m_nHatDecal = (Bui5) secondary,
            m_nTorsoDecal = (Bui5) secondary,
            m_nFeetDecal = (Bui5) secondary,
            m_nTorsoDecal2 = (Bui5) pattern,
        };
    }

    private static PetItemBehaviorTemplate GetPetItemBehaviorTemplate(uint templateId) {
        var template = s_petTemplates.GetValueOrDefault(templateId)
            ?? CoreObjectFactory.GetCoreTemplate(templateId) as GameObjectTemplate;

        return template?.m_behaviors?.OfType<PetItemBehaviorTemplate>().FirstOrDefault();
    }

    private static int GetDyeTexture(List<PetDyeToTexture> dyeToTexture, int dye)
        => dyeToTexture?.FirstOrDefault(entry => entry?.m_dye == dye)?.m_texture ?? dye;

    private static long ParseHatchRate(string hatchRateString) {
        if (string.IsNullOrEmpty(hatchRateString)) {
            return 0;
        }

        // The hatch rate will be a stirng like "360m", which means 360 minutes.
        // It also may be 'h' or 's' or 'd' for hours, seconds, or days, respectively.
        var timeMultiplier = hatchRateString.Last() switch {
            's' => 1,
            'm' => 60,
            'h' => 60 * 60,
            'd' => 60 * 60 * 24,
            _ => 0
        };

        if (timeMultiplier == 0) {
            Logger.Error("Invalid hatch rate string: {0}",
                Logger.Args(hatchRateString));

            return 0;
        }

        var timeValueString = hatchRateString[..^1];
        if (!uint.TryParse(timeValueString, out var timeValue)) {
            Logger.Error("Invalid hatch rate string: {0}",
                Logger.Args(hatchRateString));

            return 0;
        }

        // Then, the amount of seconds since epoch PLUS the hatch time will give us the hatch time for this pet.
        var hatchTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + timeValue * timeMultiplier;

        return hatchTime;
    }

    public void DisposeStream() => s_petTemplates.Clear();

}
