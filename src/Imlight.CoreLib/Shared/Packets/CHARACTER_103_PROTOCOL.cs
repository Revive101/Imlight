/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
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

        // Internal social routing always uses saved character IDs.
        public ulong RequesterCharId;
        public ulong RecipientCharId;
        public ByteString OwnerName;
        public byte OwnerLevel;
        public ByteString OwnerSchool;
        public byte Remove;

    }

    public sealed class MSG_BUDDYREQUESTREPLYFWD : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 103;

        // Internal social routing always uses saved character IDs.
        public ulong RequesterCharId;
        public ulong RecipientCharId;
        public bool Accept;
        public Relationship NewRelationship;

    }

    public sealed class MSG_BUDDYDROPFWD : IServerMessage {

        public byte MessageOrder { get; } = 11;
        public byte ServiceID { get; } = 103;

        // Internal social routing always uses saved character IDs.
        public ulong RequesterCharId;
        public ulong RecipientCharId;

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
        public object Result;

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

        /// <summary>
        /// Optional: told <see cref="TransitionMessage"/> once the goal has actually completed (after its
        /// completion dialog closes), so the NPC can re-offer only after the quest is done.
        /// </summary>
        public IActorRef TransitionTarget;
        public IServerMessage TransitionMessage;

    }

    public sealed class MSG_COMPLETEUSAGEGOAL : IServerMessage {

        public byte MessageOrder { get; } = 19;
        public byte ServiceID { get; } = 103;

        public ulong QuestID;
        public ulong GoalID;

        /// <summary>True when another goal completed by the same use already shows this goal's completion dialog.</summary>
        public bool SuppressCompletionDialog;

    }

    public sealed class MSG_RESULTHANDLERCOMPLETED : IServerMessage {

        public byte MessageOrder { get; } = 20;
        public byte ServiceID { get; } = 103;

        public bool Success { get; init; }
        public System.Type ResultType { get; init; }

    }

    /// <summary>
    /// Internal timer message: fired when a pet egg's hatch time arrives.
    /// </summary>
    public sealed class MSG_DOEGGHATCH : IServerMessage {

        public byte MessageOrder { get; } = 21;
        public byte ServiceID { get; } = 103;

        public ulong EggGlobalId;

    }

    /// <summary>
    /// Sent by the ShopService to the EquipmentService after the equipped pet item changed its look or
    /// name: the pet following its owner is replaced by one built from <see cref="PetItemId"/>.
    /// </summary>
    public sealed class MSG_RESUMMONPET : IServerMessage {

        public byte MessageOrder { get; } = 22;
        public byte ServiceID { get; } = 103;

        public ulong PetItemId;

    }

    /// <summary>
    /// Asks an online player's session to answer another player's MSG_BUDDYSTATS request from its live
    /// character, replying straight to the requester's session.
    /// </summary>
    public sealed class MSG_BUDDYSTATSFWD : IServerMessage {

        public byte MessageOrder { get; } = 23;
        public byte ServiceID { get; } = 103;

        public GAME_5_PROTOCOL.MSG_BUDDYSTATS Request;
        public ulong BuddyCharId;
        public IActorRef Requester;
        public ulong RequesterGameObjectId;

    }

    /// <summary>
    /// Internal timer message: a persona goal's completion dialog was never reported closed
    /// (MSG_COMPLETEDIALOG), so the QuestService completes the goal anyway.
    /// </summary>
    public sealed class MSG_GOALDIALOGTIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 24;
        public byte ServiceID { get; } = 103;

        public ulong QuestID;
        public ulong GoalID;

    }

    /// <summary>
    /// Sent by the ResCinematicActor handler to the player's session: the CinematicService plays it.
    /// </summary>
    public sealed class MSG_PLAYCINEMATICACTOR : IServerMessage {

        public byte MessageOrder { get; } = 25;
        public byte ServiceID { get; } = 103;

        public ResCinematicActor Cinematic;
        public IActorRef ZoneActor;

    }

    /// <summary>
    /// Internal timer message: a cinematic actor's state has run its auto transition time.
    /// </summary>
    public sealed class MSG_CINEMATICAUTOSTATE : IServerMessage {

        public byte MessageOrder { get; } = 26;
        public byte ServiceID { get; } = 103;

        public int RunId;
        public string StateName;

    }

    /// <summary>
    /// Internal timer message: a cinematic dialog was never reported closed.
    /// </summary>
    public sealed class MSG_CINEMATICDIALOGTIMEOUT : IServerMessage {

        public byte MessageOrder { get; } = 27;
        public byte ServiceID { get; } = 103;

        public int RunId;
        public string StateName;

    }

    /// <summary>
    /// Internal timer message: a finished cinematic's actor is removed from the client.
    /// </summary>
    public sealed class MSG_CINEMATICREMOVEACTOR : IServerMessage {

        public byte MessageOrder { get; } = 28;
        public byte ServiceID { get; } = 103;

        public int RunId;

    }

    /// <summary>
    /// Sent to every player's session when a trigger puts a named zone object into a state: objects only the
    /// client owns (doors, collision) learn it through a dynamic mod, as live does.
    /// </summary>
    public sealed class MSG_SENDDYNAMODSTATE : IServerMessage {

        public byte MessageOrder { get; } = 29;
        public byte ServiceID { get; } = 103;

        public string ObjectName;
        public string StateName;

    }

    /// <summary>
    /// Sent by the quest commands: the quest service runs the action through its normal goal, quest and
    /// removal paths and replies to the player with a server message.
    /// </summary>
    public sealed class MSG_QUESTCOMMAND : IServerMessage {

        public byte MessageOrder { get; } = 30;
        public byte ServiceID { get; } = 103;

        /// <summary>
        /// One of: remove, completegoal, complete, grant, forcegrant, list, info.
        /// </summary>
        public string Action;

        /// <summary>
        /// The quest name, or empty for the only active quest.
        /// </summary>
        public string QuestName;

    }

}
