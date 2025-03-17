/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty.PropertyReflection;
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

    /// <summary>
    /// Sent by a <see cref="ShopService"/> to a <see cref="IServiceComponent"/> to request a purchase of an item.
    /// </summary>
    internal sealed class MSG_SHOPBUYREQUEST : IServerMessage {

        public byte MessageOrder { get; } = 10;
        public byte ServiceID { get; } = 103;

        /// <summary>
        /// The <see cref="IActorRef"/> of the <see cref="SessionActor"/> that is making the purchase.
        /// </summary>
        public IActorRef PlayerActor;

        /// <summary>
        /// The <see cref="Wizard"/> that is making the purchase.
        /// </summary>
        public Wizard PlayerWizard;

        /// <summary>
        /// The <see cref="GID"/> of the item that is being purchased.
        /// </summary>
        public GID ItemID;

        /// <summary>
        /// The type of currency that is being used to purchase the item.
        /// </summary>
		public byte CurrencyType;

        /// <summary>
        /// The texture of the item that is being purchased, if applicable.
        /// </summary>
		public int Texture;

        /// <summary>
        /// The decal of the item that is being purchased, if applicable.
        /// </summary>
		public int Decal;

        /// <summary>
        /// The second decal of the item that is being purchased, if applicable.
        /// </summary>
		public int Decal2;

        /// <summary>
        /// The name of the pet that is being purchased, if applicable.
        /// </summary>
		public uint PetName;

        /// <summary>
        /// The global ID of the object that the item is being purchased from.
        /// </summary>
		public ulong InteractedObjectGlobalID;

        /// <summary>
        /// The quantity of the item that is being purchased.
        /// </summary>
		public uint Quantity;

    }

}
