/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Models.Player;

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
        public CoreObject WizardGameObject;

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

    public sealed class MSG_REMOVEDYNAMOD : IServerMessage {

        public byte MessageOrder { get; } = 8;
        public byte ServiceID { get; } = 103;

        public ResRemoveDynaMod DynaMod;
        public IActorRef ContextActor;

    }

    public sealed class MSG_DOENERGYTICK : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 103;

    }

    public sealed class MSG_BUDDYREQUESTADDFWD : IServerMessage {

        public byte MessageOrder { get; } = 9;
        public byte ServiceID { get; } = 103;

        public ulong ListOwnerGID;
        public ulong EntryGID;
        public ByteString OwnerName;
        public byte OwnerLevel;
        public ByteString OwnerSchool;
        public byte Remove;

    }

    public sealed class MSG_BUDDYREQUESTREPLYFWD : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 103;

        public ulong ListOwnerGID;
        public ulong EntryGID;
        public bool Accept;
        public Relationship NewRelationship;

    }

    public sealed class MSG_BUDDYDROPFWD : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 103;

        public ulong ListOwnerGID;
        public ulong EntryGID;

    }

    public sealed class MSG_GAINXP : IServerMessage {

        public byte MessageOrder { get; } = 12;
        public byte ServiceID { get; } = 103;

        public int XP;

    }

    public sealed class MSG_EXECUTERESULTS : IServerMessage {

        public byte MessageOrder { get; } = 13;
        public byte ServiceID { get; } = 103;

        public IActorRef PlayerRef;
        public CoreObject PlayerObj;
        public IActorRef ReplyTo;

    }

    public sealed class MSG_RESULTEXECUTED : IServerMessage {

        public byte MessageOrder { get; } = 14;
        public byte ServiceID { get; } = 103;

        public bool Success;

    }

    public sealed class MSG_INITIALIZEHANDLER : IServerMessage {

        public byte MessageOrder { get; } = 15;
        public byte ServiceID { get; } = 103;

        public object Context;

    }

    public sealed class MSG_EXECUTEHANDLER : IServerMessage {

        public byte MessageOrder { get; } = 16;
        public byte ServiceID { get; } = 103;

    }

    public sealed class MSG_SENDQUESTOFFERCACHEOPTION : IServerMessage {

        public byte MessageOrder { get; } = 17;
        public byte ServiceID { get; } = 103;

        public QuestTemplate Quest;

    }

    public sealed class MSG_COMPLETEPERSONAGOAL : IServerMessage {

        public byte MessageOrder { get; } = 18;
        public byte ServiceID { get; } = 103;

        public ulong QuestID;
        public ulong GoalID;

    }

}
