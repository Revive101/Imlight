using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// The spawners a character's quest results switched on (<c>ResSpawn</c>), kept in the character's
/// quest registry so they persist. The zone brings them back when the character joins a zone instance
/// again; a later <c>ResDespawn</c> of the spawner forgets it. A value of 0 means forgotten.
/// </summary>
public static class RememberedSpawns {

    private const string PREFIX = "ResSpawn:";

    private static string Key(string zone, ulong spawnId) => $"{PREFIX}{zone}:{spawnId}";

    public static void Remember(Wizard wizard, string zone, ulong spawnId) {
        if (wizard is null || string.IsNullOrEmpty(zone)) {
            return;
        }

        wizard.SetRegistryValue(Key(zone, spawnId), 1);
    }

    public static void Forget(Wizard wizard, string zone, ulong spawnId) {
        if (wizard is null || string.IsNullOrEmpty(zone)) {
            return;
        }

        var key = Key(zone, spawnId);
        if (wizard.HasRegistryValue(key) && wizard.GetRegistryValue(key) != 0) {
            wizard.SetRegistryValue(key, 0);
        }
    }

    public static List<ulong> Get(Wizard wizard, string zone) {
        var result = new List<ulong>();
        if (wizard?.QuestBehavior is null || string.IsNullOrEmpty(zone)) {
            return result;
        }

        var zonePrefix = $"{PREFIX}{zone}:";
        foreach (var (key, value) in wizard.QuestBehavior.Registry.ToArray()) {
            if (value != 0
                && key.StartsWith(zonePrefix, StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(key.AsSpan(zonePrefix.Length), out var spawnId)) {
                result.Add(spawnId);
            }
        }

        return result;
    }

}
