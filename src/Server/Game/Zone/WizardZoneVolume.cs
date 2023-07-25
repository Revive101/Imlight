using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneVolume : WizardZoneObject
{
    private readonly Volume _volume;

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume) 
        : base(activeGameObject, wizardZoneRef)
    {
        this._volume = volume;
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneVolume(activeGameObject, wizardZoneRef, volume));
    }
}