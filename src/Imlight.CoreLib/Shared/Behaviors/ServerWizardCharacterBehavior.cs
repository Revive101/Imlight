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
using Imcodec.ObjectProperty.Bit;
using Imcodec.ObjectProperty.TypeCache;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizardCharacterBehavior : IClientBehaviorProvider<WizardCharacterBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    // Can get this from ServerPetItemBehavior.
    [JsonIgnore] public eRace Race;

    public Bui2 HeadHandsModel;
    public Bui4 HairModel;
    public Bui2 HatModel;
    public Bui2 TorsoModel;
    public Bui2 FeetModel;

    public WizardCharacterBehavior GetClientBehaviorInstance() => new() {
        m_eRace = Race,
        m_nHeadHandsModel = HeadHandsModel,
        m_nHairModel = HairModel,
        m_nHatModel = HatModel,
        m_nTorsoModel = TorsoModel,
        m_nFeetModel = FeetModel
    };

}
