using System.Collections.Generic;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZonePath
{
    public GID Id { get; set; }
    public ByteString Name { get; set; }
    public List<NodeObject> Nodes { get; set; }

    public WizardZonePath(GID id, ByteString name)
    {
        this.Id = id;
        this.Name = name;
        this.Nodes = new List<NodeObject>();
    }
}