/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.Misc;

public class OnlinePlayer {
    public ushort SessionId;
    public ulong AccountId;
    public ulong CharacterId;
    public string CurrentZone;
    public string CurrentRealm;
    public string ActorPath;
}
