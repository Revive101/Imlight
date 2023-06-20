/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// This is a child actor of a <see cref="WizardZone"/> that represents a path that exists in that zone. It is also
/// responsible for spawning the creatures on interval.
/// </summary>
public class WizardZonePath : ReceiveActor
{
    public GID Id { get; init; }
    public ByteString Name { get; init; }
    public List<NodeObject> Nodes { get; init; }
    public List<SpawnObject> CreatureSpawnData { get; init; }

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