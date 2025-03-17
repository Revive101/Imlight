/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Collections;
using System;
using System.Text.Json.Serialization;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class DynamodSet {
    internal ulong CharId { get; set; }
    internal Dynamod[] Dynamods { get; set; }

    [JsonConstructor]
    public DynamodSet() { }

    // ctor
    internal DynamodSet(ulong charId) {
        CharId = charId;

        // todo: test; remove later!
        AddDynamod(new Dynamod {
            ZoneName = "WizardCity/WC_Hub",
            ClientTag = "WC_GateCommons_ToUnicornWay",
            ModState = "IdleOpen"
        });
    }

    internal bool AddDynamod(Dynamod dynamod) {
        if (Dynamods == null) {
            Dynamods = new Dynamod[1];
            Dynamods[0] = dynamod;
            return true;
        }

        for (int i = 0; i < Dynamods.Length; i++) {
            if (Dynamods[i] is null || Dynamods[i].ZoneName == dynamod.ZoneName) {
                Dynamods[i] = dynamod;
                return true;
            }
        }

        Dynamod[] resizedArray = new Dynamod[Dynamods.Length + 1];
        Array.Copy(Dynamods, resizedArray, Dynamods.Length);
        resizedArray[Dynamods.Length] = dynamod;
        Dynamods = resizedArray;

        return true;
    }

    internal bool RemoveDynamod(string clientTag) {
        if (Dynamods == null) {
            return false;
        }

        for (int i = 0; i < Dynamods.Length; i++) {
            if (Dynamods[i] is null) {
                continue;
            }

            if (Dynamods[i].ClientTag == clientTag) {
                Dynamods[i] = null;
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
