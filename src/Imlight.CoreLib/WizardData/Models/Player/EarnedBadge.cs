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
 * BADGE STATE
 * ========================================================================
 *
 * PURPOSE:
 * One completed badge of a character: when it was completed and its place in the award order.
 *
 * USAGE EXAMPLE:
 * wizard.BadgeBehavior.Earned[badgeId] = new EarnedBadge { AwardSequence = 1, CompletedAtUnix = now };
 *
 * NOTE:
 *
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.WizardData.Models.Player;

public class EarnedBadge {

    public uint AwardSequence { get; set; }
    public long CompletedAtUnix { get; set; }

}
