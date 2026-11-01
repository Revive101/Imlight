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
 * A badge the client has been told about and when; the entries back the client's New tab.
 *
 * USAGE EXAMPLE:
 * wizard.BadgeBehavior.NewBadges.Add(new NewBadgeEntry { BadgeId = id, Time = now });
 *
 * NOTE:
 * The client never removes entries from this list, so the server expires them.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.WizardData.Models.Player;

public class NewBadgeEntry {

    public uint BadgeId { get; set; }
    public uint Time { get; set; }

}
