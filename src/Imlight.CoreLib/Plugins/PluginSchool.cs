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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/27/2026
 */

using System;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

internal static class PluginSchool {

    /// <summary>
    /// The plugin API's <see cref="School"/> for a school, or <see cref="School.None"/> if it has none.
    /// </summary>
    public static School ToPluginSchool(this MagicSchool school) => school switch {
        MagicSchool.Fire    => School.Fire,
        MagicSchool.Ice     => School.Ice,
        MagicSchool.Storm   => School.Storm,
        MagicSchool.Myth    => School.Myth,
        MagicSchool.Life    => School.Life,
        MagicSchool.Death   => School.Death,
        MagicSchool.Balance => School.Balance,
        MagicSchool.Sun     => School.Sun,
        MagicSchool.Moon    => School.Moon,
        MagicSchool.Star    => School.Star,
        MagicSchool.Shadow  => School.Shadow,
        _                   => School.None,
    };

    /// <summary>
    /// The plugin API's <see cref="School"/> for a school's name in a template, or <see cref="School.None"/>
    /// if it names none.
    /// </summary>
    public static School ToPluginSchool(string schoolName)
        => Enum.TryParse<MagicSchool>(schoolName, out var school) ? school.ToPluginSchool() : School.None;

}
