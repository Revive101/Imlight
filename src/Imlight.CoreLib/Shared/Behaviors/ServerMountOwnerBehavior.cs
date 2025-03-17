/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerMountOwnerBehavior : IClientBehaviorProvider<ClientMountOwnerBehavior> {
    [JsonIgnore] public bool NoTransfer { get; set; } = false;

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

        var mountBehavior = mountTemplate.m_behaviors.OfType<MountItemBehaviorTemplate>().FirstOrDefault();
        if (mountBehavior == null) {
            return false;
        }

        MountRace = mountBehavior.m_eRace;
        MountGender = mountBehavior.m_eGender;
        MountType = mountBehavior.m_eMountType;
        MountHasAdjustableAnimationRate = mountBehavior.m_adjustableAnimationRate;
        MountGeometryOption = mountBehavior.m_geometryOption;
        LastMountId = new GID(item.m_templateID);

        MountPatternColor = GetColor(mountBehavior.m_patternToTexture, item.m_pattern);
        MountPrimaryColor = GetColor(mountBehavior.m_primaryDyeToTexture, item.m_primaryColor);
        MountSecondaryColor = GetColor(mountBehavior.m_secondaryDyeToTexture, item.m_secondaryColor);

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

    private static int GetColor(IList<MountDyeToTexture> textureMappings, int colorIndex) {
        if (textureMappings != null && textureMappings.Count > 0) {
            if (colorIndex >= 0 && colorIndex < textureMappings.Count) {
                return textureMappings[colorIndex].m_texture;
            }
        }

        return colorIndex;
    }

    public ClientMountOwnerBehavior GetClientBehaviorInstance() => new() {
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
