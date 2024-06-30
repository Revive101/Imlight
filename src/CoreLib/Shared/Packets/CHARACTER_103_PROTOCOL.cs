/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Shared.Packets;

public class CHARACTER_103_PROTOCOL : IServerProtocol {
    public byte ServiceID { get; } = 103;
    public string ProtocolType { get; } = "CHARACTER";
    public int ProtocolVersion { get; } = 1;
    public string ProtocolDescription { get; } = "Internal Character General Messages.";

    public class MSG_SETACTIVEWIZARD : IServerMessage {
        public byte MessageOrder { get; } = 1;
        public byte ServiceID { get; } = 103;

        public Wizard Wizard;
    }

    public class MSG_QUERYACTIVEWIZARD : IServerMessage {
        public byte MessageOrder { get; } = 2;
        public byte ServiceID { get; } = 103;
    }

    public class MSG_CHARACTER : IServerMessage {
        public byte MessageOrder { get; } = 3;
        public byte ServiceID { get; } = 103;

        public Wizard Wizard;
        public TypeCache.CoreObject WizardGameObject;
    }

    public class MSG_LEVELUP : IServerMessage {
        public byte MessageOrder { get; } = 4;
        public byte ServiceID { get; } = 103;

        public byte NewLevel;
    }

    public sealed class MSG_DOTELEPORTEFFECTS : IServerMessage {
        public byte MessageOrder { get; } = 5;
        public byte ServiceID { get; } = 103;
    }

    public sealed class MSG_ENTERSTATE : IServerMessage {
        public byte MessageOrder { get; } = 6;
        public byte ServiceID { get; } = 103;

        public string StateName;
    }

    public sealed class MSG_ADDDYNAMOD : IServerMessage {
        public byte MessageOrder { get; } = 7;
        public byte ServiceID { get; } = 103;

        public ResAddDynaMod DynaMod;
        public IActorRef ContextActor;
    }
}
