using Akka.Actor;
using Imlight.Server.Shared.Networking;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneObject : ReceiveProtocolDispatcher
{
    protected CoreObject ActiveGameObject;
    protected IActorRef WizardZoneRef;
    
    // ctor
    public WizardZoneObject(CoreObject activeGameObject, IActorRef wizardZoneRef)
    {
        this.ActiveGameObject = activeGameObject;
        this.WizardZoneRef = wizardZoneRef;
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, wizardZoneRef));
    }
}