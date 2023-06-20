using Akka.Actor;
using Imlight.Server.Shared.Networking;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneObject : ReceiveProtocolDispatcher
{
    private CoreObject _activeGameObject;
    
    // ctor
    public WizardZoneObject(CoreObject activeGameObject)
    {
        this._activeGameObject = activeGameObject;
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObject(activeGameObject));
    }
}