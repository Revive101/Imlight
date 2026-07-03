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

    private const uint GENERIC_PET_TEMPLATE_ID = 2;

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

    public static WizClientPet CreatePetGameObject(WizClientObjectItem pet) {
        var genericPetObject = new WizClientPet();
        CoreObjectFactory.InitializeCoreObjectBehaviors(genericPetObject, GENERIC_PET_TEMPLATE_ID);

        genericPetObject.m_globalID = pet.m_globalID;
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

        return genericPetObject;
    }

    private static WizClientPet SetPetGameObjectBehaviors(WizClientPet petGameObject, WizClientObjectItem pet) {
        // This single behavior on the pet can help us build both of the behaviors
        // on the game object.
        if (!CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(pet, out var petNameBehaviorInstanceOnPet)) {
            Logger.Error("Pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(pet.m_globalID.Full, nameof(ClientPetNameBehavior)));

            return null;
        }

        // Replace the generic pet's behaviors with the pet's behaviors.
        if (CoreObjectFactory.FindBehaviorInstance<ClientPetNameBehavior>(petGameObject, out var petNameBehaviorInstance)) {
            // Now find the behavior instance on the pet and replace the generic pet's behavior with it.
            var idx = petGameObject.m_inactiveBehaviors.IndexOf(petNameBehaviorInstance);
            // Flat replace with the pet's behavior instance.
            petGameObject.m_inactiveBehaviors[idx] = petNameBehaviorInstanceOnPet;
        }
        else {
            Logger.Error("Generic pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(petGameObject.m_globalID.Full, nameof(ClientPetNameBehavior)));
        }

        // Find the game object's WizardCharacterBehavior and create a new one that sets the race and gender.
        if (CoreObjectFactory.FindBehaviorInstance<WizardCharacterBehavior>(petGameObject, out var wizardCharacterBehaviorInstance)) {
            // Now find the behavior instance on the pet and replace the generic pet's behavior with it.
            var idx = petGameObject.m_inactiveBehaviors.IndexOf(wizardCharacterBehaviorInstance);
            // Flat replace with the pet's behavior instance.
            petGameObject.m_inactiveBehaviors[idx] = new WizardCharacterBehavior() {
                m_eRace = petNameBehaviorInstanceOnPet.m_eRace,
                m_eGender = petNameBehaviorInstanceOnPet.m_eGender,
            };
        }
        else {
            Logger.Error("Generic pet {0} should've contained behavior {1}, but it did not.",
                Logger.Args(petGameObject.m_globalID.Full, nameof(WizardCharacterBehavior)));
        }

        return petGameObject;
    }

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
