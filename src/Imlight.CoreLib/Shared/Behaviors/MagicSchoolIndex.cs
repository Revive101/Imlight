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
 * MAGIC SCHOOL INDEX
 * ========================================================================
 * 
 * PURPOSE:
 * The small school number the client's social panels use (group roster,
 * friends list) instead of the school name hash.
 * 
 * USAGE EXAMPLE:
 * var schoolId = MagicSchoolIndex.ToSocialIndex(wizard.MagicSchoolBehavior.MagicSchool);
 * 
 * NOTE:
 * The client maps 1 Balance, 2 Death, 3 Fire, 4 Ice, 5 Life, 6 Myth,
 * 7 Storm to its school icons; 0 shows no icon.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

namespace Imlight.CoreLib.Shared.Behaviors;

internal static class MagicSchoolIndex {

    public static uint ToSocialIndex(MagicSchool school) => school switch {
        MagicSchool.Balance => 1,
        MagicSchool.Death => 2,
        MagicSchool.Fire => 3,
        MagicSchool.Ice => 4,
        MagicSchool.Life => 5,
        MagicSchool.Myth => 6,
        MagicSchool.Storm => 7,
        _ => 0
    };

}
