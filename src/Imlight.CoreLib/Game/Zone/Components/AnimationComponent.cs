/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 * 
 * ========================================================================
 * ANIMATION COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages animation state changes for zone entities, allowing for dynamic
 * visual effects and interactions.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Supports different animation states for different entity types.
 * 
 * TODO:
 * - Improve state change information sourcing
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 8/25/2025
 */

using System;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class AnimationComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && template.m_behaviors.Any(x => x is AnimationBehaviorTemplate);

    public override void OnAwake() {

    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTERSTATE))]
    public void ReceiveEnterState(ZONE_102_PROTOCOL.MSG_ENTERSTATE msg) {
        // The behavior template has "m_datalookupassetname = AnimationData/myObj.xml"
        // In theory, we could use this to know what animations are possible for this object
        // to play.
        if (Entity.Template is not GameObjectTemplate goTemplate) {
            return;
        }

        if (msg.ObjectName == goTemplate.m_objectName) {
            // If the name matches, we can be fairly certain this message is for us.
            Entity.ChangeStateExclusiveSender(msg.StateName, msg.Sender);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEOBJECT))]
    public void ReceiveRemoveObject(ZONE_102_PROTOCOL.MSG_REMOVEOBJECT msg) {
        if (Entity.Template is not GameObjectTemplate goTemplate) {
            return;
        }

        if (msg.ObjectName == goTemplate.m_objectName) {
            Entity.DespawnObject();
        }
    }

}