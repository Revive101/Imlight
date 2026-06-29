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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;

namespace Imlight.CoreLib.WizardData.Models.Pet;

public record PetObjectItem : WizClientObjectItem {

    public ulong OwnerId { get; set; }
    public uint TemplateId { get; set; }
    public ServerPetNameBehavior ServerPetNameBehavior { get; set; }
    public ServerWizardCharacterBehavior ServerWizardCharacterBehavior { get; set; }
    public ServerPetItemBehavior ServerPetItemBehavior { get; set; }

    // Paramless ctor for deserialization.
    // Must be initialized by calling Initialize() after deserialization.
    public PetObjectItem() { }

    // ctor: Called by PetFactory when creating a new pet.
    public PetObjectItem(ulong ownerId, uint templateId) {
        OwnerId = ownerId;
        TemplateId = templateId;
        ServerPetNameBehavior = new ServerPetNameBehavior();
        ServerWizardCharacterBehavior = new ServerWizardCharacterBehavior();
        ServerPetItemBehavior = new ServerPetItemBehavior();
    }

    internal CoreObject ToClientObject() => new WizClientObjectItem {
        m_globalID = m_globalID,
        m_templateID = m_templateID,
        m_characterId = m_characterId,
        m_inactiveBehaviors = [
                ServerPetItemBehavior.GetClientBehaviorInstance(),
            ]
    };

    public void Initialize() {
        // We can't initialize this in the constructor because the properties will not yet
        // be loaded.
        ServerPetNameBehavior ??= new ServerPetNameBehavior();
        ServerWizardCharacterBehavior ??= new ServerWizardCharacterBehavior();
        ServerPetItemBehavior ??= new ServerPetItemBehavior();

        ServerPetNameBehavior.Gender = ServerPetItemBehavior.Gender;
        ServerPetNameBehavior.Race = ServerPetItemBehavior.Race;
        ServerPetNameBehavior.TemplateID = TemplateId;
        ServerPetNameBehavior.PetLevel = ServerPetItemBehavior.Level;

        ServerWizardCharacterBehavior.Race = ServerPetItemBehavior.Race;
    }

}