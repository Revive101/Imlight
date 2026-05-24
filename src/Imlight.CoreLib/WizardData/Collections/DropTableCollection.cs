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

using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class DropTableCollection {

    /// <summary>
    /// Retrieves a drop table by name.
    /// </summary>
    /// <param name="tableName">The name of the drop table to retrieve.</param>
    /// <returns>The drop table, or null if not found.</returns>
    public static DropTable GetDropTable(string tableName) 
        => SpiralDB.GetDropTable(tableName);

}
