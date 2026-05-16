/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class DynamodSet {

    internal ulong CharId { get; set; }
    internal List<Dynamod> Dynamods { get; set; }

    [JsonConstructor]
    public DynamodSet() { }

    // ctor
    internal DynamodSet(ulong charId) {
        CharId = charId;
        Dynamods = [];
    }

    internal bool AddDynamod(Dynamod dynamod) {
        if (Dynamods == null) {
            Dynamods = [dynamod];

            return true;
        }

        // Check to see if the zone name and client tag already exists.
        // If it does, overwrite it.
        foreach (var existingDynamod in Dynamods) {
            if (existingDynamod is null) {
                continue;
            }

            if (existingDynamod.ZoneName.Equals(dynamod.ZoneName, StringComparison.OrdinalIgnoreCase)
                && existingDynamod.ClientTag.Equals(dynamod.ClientTag, StringComparison.OrdinalIgnoreCase)) {
                existingDynamod.ModState = dynamod.ModState;

                return true;
            }
        }

        Dynamods.Add(dynamod);

        return true;
    }

    internal bool RemoveDynamod(string clientTag) {
        if (Dynamods == null) {
            Dynamods = [];

            return false;
        }

        foreach (var existingDynamod in Dynamods) {
            if (existingDynamod is null) {
                continue;
            }

            if (existingDynamod.ClientTag.Equals(clientTag, StringComparison.OrdinalIgnoreCase)) {
                Dynamods.Remove(existingDynamod);

                return true;
            }
        }

        return false;
    }
    
}

[Serializable]
public class Dynamod {

    internal string ZoneName { get; set; }
    internal string ClientTag { get; set; }
    internal string ModState { get; set; }

}
