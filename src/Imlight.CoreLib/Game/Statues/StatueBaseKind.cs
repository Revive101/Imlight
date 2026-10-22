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
 * STATUE BASE KIND
 * ========================================================================
 *
 * PURPOSE:
 * Says how a statue's base template can be shown to the client.
 *
 * USAGE EXAMPLE:
 * Returned by StatueLook.GetBaseKind.
 *
 * NOTE:
 * None means the template cannot be a statue base.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

namespace Imlight.CoreLib.Game.Statues;

internal enum StatueBaseKind {

    None,
    WizardNpc,

}
