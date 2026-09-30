using System.Collections.Generic;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

/// <summary>
/// Builds MSG_DYNAMODBEHAVIOR_UPDATEMODS messages from a player's persisted dynamods.
/// A mod with a state is a <see cref="DynaModDelta"/>; a mod without one is a plain <see cref="DynaMod"/>.
/// </summary>
internal static class DynaModMessages {

    // Only the transmitted properties are on the wire (verified byte for byte against a live capture).
    private const PropertyFlags WIRE_FLAGS = PropertyFlags.Prop_Transmit;

    // Live opens the full list with an empty mod at index 1, then numbers the mods after it.
    private const int SENTINEL_INDEX = 1;

    private static DynaMod ToWireMod(Dynamod mod, int index) {
        if (string.IsNullOrEmpty(mod.ModState)) {
            return new DynaMod { m_clientTag = mod.ClientTag ?? string.Empty, m_index = index };
        }

        return new DynaModDelta {
            m_clientTag = mod.ClientTag ?? string.Empty,
            m_index = index,
            m_stateID = StringHash.Compute(mod.ModState),
        };
    }

    private static bool TrySerialize(PropertyClass obj, out ByteString data)
        => new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None)
            .Serialize(obj, WIRE_FLAGS, out data);

    /// <summary>The login message that carries every persisted mod.</summary>
    public static GAME_5_PROTOCOL.MSG_DYNAMODBEHAVIOR_UPDATEMODS UpdateAll(ulong globalId, IEnumerable<Dynamod> mods) {
        var list = new DynaModList { m_allMods = [new DynaMod { m_clientTag = string.Empty, m_index = SENTINEL_INDEX }] };
        var index = SENTINEL_INDEX;
        foreach (var mod in mods.Where(m => m is not null)) {
            list.m_allMods.Add(ToWireMod(mod, ++index));
        }

        return TrySerialize(list, out var data)
            ? new GAME_5_PROTOCOL.MSG_DYNAMODBEHAVIOR_UPDATEMODS { GlobalID = globalId, UpdateAll = 1, AllMods = data }
            : null;
    }

    /// <summary>The message for one newly added mod.</summary>
    public static GAME_5_PROTOCOL.MSG_DYNAMODBEHAVIOR_UPDATEMODS Add(ulong globalId, Dynamod mod, int index)
        => TrySerialize(ToWireMod(mod, index), out var data)
            ? new GAME_5_PROTOCOL.MSG_DYNAMODBEHAVIOR_UPDATEMODS { GlobalID = globalId, Add = 1, NewMod = data, Index = 0 }
            : null;

}
