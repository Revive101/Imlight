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
 * TRADE SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * The status codes of MSG_TRADE_RESULT. The client maps each one to a locale line, so the
 * numbers are the contract.
 * 
 * USAGE EXAMPLE:
 * SendToSocket(new WIZARD_12_PROTOCOL.MSG_TRADE_RESULT { Status = (uint) TradeError.CannotHold });
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.Game.Trading;

internal enum TradeError : uint {
    None = 0,
    NullSession = 1,
    ZonePlayerNotFound = 2,
    ZoneNotFromPlayer = 3,
    TargetNotInZone = 4,
    TargetNotFound = 5,
    TargetNotInZoneRepeat = 6,
    TargetAlreadyTrading = 7,
    Failed = 8,
    InstanceNotValid = 9,
    NotJoined = 10,
    NotReady = 11,
    AlreadyTrading = 12,
    CannotHold = 13,
    CannotTradeNow = 14,
    NotAccepting = 15,
    OtherPlatform = 16,
    OtherPlatformRepeat = 17,
}
