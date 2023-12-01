/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class ChatLog {
    public DateTime TimeStamp { get; set; }
    public string ZoneName { get; set; }
    public ulong CharacterId { get; set; }
    public ulong AccountId { get; set; }
    public string Message { get; set; }
}
