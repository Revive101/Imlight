using Akka.Actor;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
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
        this.Template = CoreObjectFactory.GetTemplate<CoreTemplate>(activeGameObject.m_templateID);
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject, wizardZoneRef));
    }
}