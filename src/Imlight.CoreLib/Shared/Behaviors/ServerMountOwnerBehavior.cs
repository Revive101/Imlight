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

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Newtonsoft.Json;

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
