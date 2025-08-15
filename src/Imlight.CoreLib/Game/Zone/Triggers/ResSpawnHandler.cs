/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Resources;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.CoreObject;
using Imcodec.ObjectProperty;
using Imcodec.MessageLayer.Generated;
using System.Collections.Generic;
using System;

namespace Imlight.CoreLib.Game.Zone.Triggers;

internal sealed class ResSpawnHandler<T> : BaseResultHandler<ResSpawn> where T : Result {
    private readonly ulong _templateID;
    private readonly List<NodeObject> _nodes;
    private readonly CoreObjectSerializer _serializer = new CoreObjectSerializer(
        behaviors: SerializerFlags.None
    );
    // .OnBehaviors(SerializerOptions.Behaviors.None)
    // .OnPropertyMask(SerializerOptions.PropertyFlags.Public
    //     | SerializerOptions.PropertyFlags.Transmit
    //     | SerializerOptions.PropertyFlags.AuthorityTransmit);

    // ctor
    public ResSpawnHandler(ZoneTrigger trigger) : base(trigger) {
        _templateID = Result.templateID;
        _nodes = Result.nodes;
    }

    public override void Execute(IActorRef playerRef, CoreObject playerObj)  {
        if (_templateID == 0) {
            return;
        }
        
        var coreObject = CoreObjectFactory.FinalizeCoreObject(_templateID);
        coreObject.m_location = _nodes[0].m_location;
        var Z = (float) (_nodes[0].m_direction * (Math.PI / 180));
        coreObject.m_orientation = new Imcodec.Math.Vector3(0, 0, Z);
        var propFlags = PropertyFlags.Prop_Public | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        _serializer.Serialize(coreObject, propFlags, out var data);
        var newObjectMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = data };
        playerRef.Tell(newObjectMsg);
    }
}