using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZonePath : ReceiveActor
{
    public GID Id { get; set; }
    public ByteString Name { get; set; }
    public List<NodeObject> Nodes { get; set; }
    public List<SpawnObject> CreatureSpawnData { get; set; }

    // ctor
    public WizardZonePath(GID id, ByteString name, List<NodeObject> nodes, List<SpawnObject> creatures)
    {
        this.Id = id;
        this.Name = name;
        this.Nodes = nodes;
        this.CreatureSpawnData = creatures;
    }
    
    // Akka.NET ctor
    public static Props Props(GID id, ByteString name, List<NodeObject> nodes, List<SpawnObject> creatures)
    {
        return Akka.Actor.Props.Create(() => new WizardZonePath(id, name, nodes, creatures));
    }
}