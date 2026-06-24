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
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerPetNameBehavior : IClientBehaviorProvider<ClientPetNameBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public uint NameIndices;
    public WideByteString NameOverride;
    public bool HideName;

    // These properies can be sourced from other behaviors.
    [JsonIgnore] public eGender Gender;
    [JsonIgnore] public eRace Race;
    [JsonIgnore] public uint OverallRating;
    [JsonIgnore] public uint ActiveRating;
    [JsonIgnore] public uint PetLevel;
    [JsonIgnore] public bool HasSocketedJewel;
    [JsonIgnore] public uint TemplateID;

    public ClientPetNameBehavior GetClientBehaviorInstance() => new() {
        // Base (ClientWizPlayerNameBehavior) fields
        m_nameKeys = NameIndices,
        m_eGender = Gender,
        m_eRace = Race,
        m_wsNameOverride = NameOverride,
        // Pet-specific fields
        m_overallRating = OverallRating,
        m_activeRating = ActiveRating,
        m_petLevel = PetLevel,
        m_bHasSocketedJewel = HasSocketedJewel,
        m_hideName = HideName,
        m_templateID = TemplateID
    };

}
