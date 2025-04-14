/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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