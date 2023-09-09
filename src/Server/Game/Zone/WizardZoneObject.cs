using Akka.Actor;
using Imlight.Common.Serializable;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// This is a zone object which manages itself as an actor.
/// </summary>
public class WizardZoneObject : ReceiveProtocolDispatcher
{
    protected readonly CoreObject ActiveGameObject;
    protected readonly CoreTemplate Template;
    protected readonly IActorRef WizardZoneRef;
    
    // ctor
    public WizardZoneObject(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
    {
        this.ActiveGameObject = activeGameObject;
        this.Template = template;
        this.WizardZoneRef = wizardZoneRef;
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, template, wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected virtual void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        var serializer = new CoreObjectSerializer()
            .WithSerializerFlags(ObjectSerializer.SerializerFlags.None)
            .WithPropertyFlags(ObjectSerializer.PropertyFlags.Public 
                               | ObjectSerializer.PropertyFlags.Transmit 
                               | ObjectSerializer.PropertyFlags.AuthorityTransmit);
        var msg = new GAME_5_PROTOCOL.MSG_NEWOBJECT { Data = serializer.Serialize(ActiveGameObject) };
        message.Player.Tell(msg);
        
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected virtual void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
    {
        var msg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = ActiveGameObject.m_globalID };
        message.Player.Tell(msg);
        
        Sender.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP());
    }
}