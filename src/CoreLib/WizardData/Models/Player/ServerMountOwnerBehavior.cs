/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using System;
using System.Linq;
using System.Text.Json.Serialization;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerMountOwnerBehavior : BehaviorInstance, IClientBehaviorProvider<ClientMountOwnerBehavior> {
    public eGender MountGender;
    public eRace MountRace;
    public int MountPrimaryColor;
    public int MountSecondaryColor;
    public int MountPatternColor;

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
            MountPrimaryColor = item.m_primaryColor;
            MountSecondaryColor = item.m_secondaryColor;
            MountPatternColor = item.m_pattern;
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

    public ClientMountOwnerBehavior GetClientBehaviorInstance() {
        return new ClientMountOwnerBehavior {
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
}
