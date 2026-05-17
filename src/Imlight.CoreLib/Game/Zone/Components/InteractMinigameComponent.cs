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
 *
 * ========================================================================
 * INTERACT MINIGAME 
 * ========================================================================
 * 
 * PURPOSE:
 * Manages interaction logic for minigame kiosk NPCs in the game world, 
 * providing service options for player interactions.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractMinigameComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "KioskService";
    public string NpcIcon         => "GUI/Art/Art_Quest_Minigame.dds";
    public string NpcNameKey      => "GUI_00002918";
    public string NpcTextKey      => "GUI_PlayMini";
    public WizBangs WizBang       => WizBangs.None;
    public string StateName       => null;
    public string InteractWizBang => null;
    public string DisplayKey      => "GUI_Kiosk";

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_objectName.ToString() == "WC_MiniGameKiosk";

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) 
        => [
            new KioskOption {
                m_displayKey = DisplayKey,
                m_iconKey = "Kiosk",
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var msg = new WIZARD_12_PROTOCOL.MSG_MINIGAMEKIOSK {
            GlobalID = Entity.ActiveGameObject.m_globalID
        };
        playerActor.Tell(msg);
    }

}