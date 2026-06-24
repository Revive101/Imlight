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
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Pet;

public class PetFactory : RootDirectoryResourceSingleton<PetFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "ObjectData/Pets/";

    private static readonly Dictionary<uint, WizItemTemplate> s_petTemplates = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var count = 0;

        foreach (var (fileRecord, fileStream) in base.Files) {
            if (!serializer.Deserialize<WizItemTemplate>(fileStream?.ToArray(), out var template)) {
                Logger.Error("Could not deserialize {0} as {1}",
                    Logger.Args(fileRecord.FileName, nameof(WizItemTemplate)));

                continue;
            }

            var key = template.m_templateID;
            s_petTemplates[key] = template;
            count++;
        }

        Logger.Information("Loaded {0} pet templates.",
            Logger.Args(count));
    }

    public static WizardData.Models.Pet.PetObjectItem CreateHatchedPet(ulong ownerId, uint templateId)
        => CreatePet(ownerId, templateId, preHatch: true);

    public static WizardData.Models.Pet.PetObjectItem CreatePet(
            ulong ownerId,
            uint templateId,
            bool preHatch = false
    ) {
        if (!s_petTemplates.TryGetValue(templateId, out var template)) {
            Logger.Error("Could not find pet template with ID {0}",
                Logger.Args(templateId));

            return null;
        }

        // Get the avatar info for this pet, which is important for gender and race.
        var avatarInfo = template.m_behaviors.FirstOrDefault(s => s.m_behaviorName == "PetItemBehavior");
        if (avatarInfo == null) {
            Logger.Error("Pet template with ID {0} has no avatar info.",
                Logger.Args(templateId));

            return null;
        }
        if (avatarInfo is not PetItemBehaviorTemplate petItemBehaviorTemplate) {
            // WHAT !!!
            Logger.Error("Pet template with ID {0} has avatar info that is not a petItemBehaviorTemplate.",
                Logger.Args(templateId));

            return null;
        }

        var pet = new WizardData.Models.Pet.PetObjectItem(ownerId, templateId);
        pet.ServerPetNameBehavior.TemplateID = template.m_templateID;
        pet.ServerPetNameBehavior.Race = petItemBehaviorTemplate.m_eRace;
        pet.ServerPetNameBehavior.Gender = petItemBehaviorTemplate.m_eGender;
        pet.ServerWizardCharacterBehavior.Race = petItemBehaviorTemplate.m_eRace;

        // The pet item behavior template will tell us the hatch time for this pet.
        // Or, this might be a pre-hatched pet if parameters indicate so.
        var hatchRateString = petItemBehaviorTemplate.m_sHatchRate;
        pet.ServerPetItemBehavior.Level = (byte) (preHatch ? 1 : 0);
        pet.ServerPetItemBehavior.HatchedTimeInSeconds = (uint) (preHatch ? 0 : ParseHatchRate(hatchRateString));

        return pet;
    }

    public static WizClientPet CreatePetGameObject(WizClientObjectItem pet) {
        var clientPet = new WizClientPet();
        CoreObjectFactory.InitializeCoreObjectBehaviors(clientPet, pet.m_templateID);

        return clientPet;
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
