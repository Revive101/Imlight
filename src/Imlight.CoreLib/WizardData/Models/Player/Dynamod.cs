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
