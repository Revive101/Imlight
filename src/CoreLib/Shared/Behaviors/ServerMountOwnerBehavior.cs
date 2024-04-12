/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerMountOwnerBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    [JsonIgnore] public eGender MountGender;
    [JsonIgnore] public eRace MountRace;
    [JsonIgnore] public int MountPrimaryColor;
    [JsonIgnore] public int MountSecondaryColor;
    [JsonIgnore] public int MountPatternColor;
    [JsonIgnore] public GID LastMountId;
    [JsonIgnore] public eMountType MountType;
    [JsonIgnore] public bool MountHasAdjustableAnimationRate;
    [JsonIgnore] public int MountGeometryOption;

    public bool EquipMount(WizItemTemplate mountTemplate, WizClientObjectItem item) {
        if (mountTemplate == null) {
            return false;
        }

        // MountItemBehaviorTemplate
        if (mountTemplate.m_behaviors.Any(x => x is MountItemBehaviorTemplate)) {
            var mountBehavior = mountTemplate.m_behaviors.First(x => x is MountItemBehaviorTemplate) as MountItemBehaviorTemplate;

            MountRace = mountBehavior.m_eRace;
            MountGender = mountBehavior.m_eGender;
            MountType = mountBehavior.m_eMountType;
            MountHasAdjustableAnimationRate = mountBehavior.m_adjustableAnimationRate;
            MountGeometryOption = mountBehavior.m_geometryOption;

            // If the mount has a different texture rather than color, use that instead.
            // Otherwise, use the color given from the item.
            if (mountBehavior.m_patternToTexture is not null && mountBehavior.m_patternToTexture.Count > 0) {
                MountPatternColor = mountBehavior.m_patternToTexture[0].m_texture;
            }
            else {
                MountPatternColor = item.m_pattern;
            }

            if (mountBehavior.m_primaryDyeToTexture is not null && mountBehavior.m_primaryDyeToTexture.Count > 0) {
                MountPrimaryColor = mountBehavior.m_primaryDyeToTexture[0].m_texture;
            }
            else {
                MountPrimaryColor = item.m_primaryColor;
            }

            if (mountBehavior.m_secondaryDyeToTexture is not null && mountBehavior.m_secondaryDyeToTexture.Count > 0) {
                MountSecondaryColor = mountBehavior.m_secondaryDyeToTexture[0].m_texture;
            }
            else {
                MountSecondaryColor = item.m_secondaryColor;
            }
        }

        return true;
    }

    public void UnequipMount() {
        MountRace = 0;
        MountHasAdjustableAnimationRate = false;
        MountGeometryOption = 0;
        MountPrimaryColor = 0;
        MountSecondaryColor = 0;
        MountPatternColor = 0;
    }

    public override ClientMountOwnerBehavior GetClientBehaviorInstance() => new() {
        m_gender = MountGender,
        m_race = MountRace,
        m_eMountType = MountType,
        m_primaryColor = MountPrimaryColor,
        m_secondaryColor = MountSecondaryColor,
        m_patternColor = MountPatternColor,
        m_adjustableAnimationRate = MountHasAdjustableAnimationRate,
        m_geometryOption = MountGeometryOption,
        m_lastMountID = LastMountId
    };
}
