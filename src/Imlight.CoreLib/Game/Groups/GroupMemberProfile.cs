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
 * GROUP SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * The details a group shows about one member, captured by the member's own
 * session so the directory never reads another session's wizard.
 * 
 * USAGE EXAMPLE:
 * var profile = GroupMemberProfile.FromWizard(wizard, zoneActor);
 * 
 * NOTE:
 * SchoolId is the client's social school number (see MagicSchoolIndex),
 * not the school name hash the rest of the server uses.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Groups;

internal sealed record GroupMemberProfile(
    ulong CharId,
    ulong GameObjectId,
    byte[] NameBlob,
    uint SchoolId,
    uint Level,
    string ZoneDisplayName,
    string ZonePath,
    IActorRef ZoneActor) {

    public static GroupMemberProfile FromWizard(Wizard wizard, IActorRef zoneActor) {
        var nameHex = wizard.PlayerNameBehavior.GetWizardNameAsByteHexString();

        return new GroupMemberProfile(
            CharId: wizard.CharId,
            GameObjectId: wizard.GameObjectID,
            NameBlob: DataManipulation.SpacedHexStringToBytes(nameHex),
            SchoolId: MagicSchoolIndex.ToSocialIndex(wizard.MagicSchoolBehavior.MagicSchool),
            Level: (uint) wizard.MagicSchoolBehavior.Level,
            ZoneDisplayName: wizard.ZoneDisplayName ?? string.Empty,
            ZonePath: wizard.Zone ?? string.Empty,
            ZoneActor: zoneActor
        );
    }

}
