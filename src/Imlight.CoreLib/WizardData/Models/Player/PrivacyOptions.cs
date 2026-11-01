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
 *
 * ========================================================================
 * PRIVACY OPTIONS
 * ========================================================================
 * 
 * PURPOSE:
 * The seven privacy switches of the client's settings window, saved with the character.
 * 
 * USAGE EXAMPLE:
 * if (wizard.PrivacyOptions.AllowTradeRequest == 0) { ... }
 * 
 * NOTE:
 * Each value is 0 or 1, as the client sends it.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.WizardData.Models.Player;

public sealed class PrivacyOptions {

    public byte AllowFriendRequest { get; set; } = 1;
    public byte AllowFriendTeleport { get; set; } = 1;
    public byte AllowTradeRequest { get; set; } = 1;
    public byte AllowHatchRequest { get; set; } = 1;
    public byte AllowPartyInvites { get; set; } = 1;
    public byte HidePVPEnemyChat { get; set; } = 1;
    public byte LimitHomeToFriends { get; set; }

}
