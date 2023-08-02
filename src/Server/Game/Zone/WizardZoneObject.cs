using Akka.Actor;
using Imlight.Common.Serializable;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
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
    public WizardZoneObject(CoreObject activeGameObject, IActorRef wizardZoneRef)
    {
        this.ActiveGameObject = activeGameObject;
        this.WizardZoneRef = wizardZoneRef;

        if (activeGameObject.m_templateID == 0)
        {
            Log.Warning("{WizardZoneObject} {ActiveGameObjectMDebugName} was loaded with a template ID of 0.", 
                Log.Args(nameof(WizardZoneObject), activeGameObject.m_debugName));
            return;
        }
        this.Template = CoreObjectFactory.GetCoreTemplate(activeGameObject.m_templateID);
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, wizardZoneRef));
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