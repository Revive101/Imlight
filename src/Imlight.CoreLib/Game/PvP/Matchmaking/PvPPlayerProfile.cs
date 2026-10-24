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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * The details the PvP director keeps about one player, captured by the player's own session so the
 * director never reads another session's wizard.
 * 
 * USAGE EXAMPLE:
 * var profile = PvPPlayerProfile.FromWizard(wizard, zoneActor, realmName);
 * 
 * NOTE:
 * SchoolId is the client's social school number (see MagicSchoolIndex). Location is where the player stood
 * when the session announced itself: the place a finished match sends the player back to.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal sealed record PvPPlayerProfile(
    ulong CharId,
    ulong GameObjectId,
    byte[] NameBlob,
    uint SchoolId,
    uint Level,
    string RealmName,
    string ZonePath,
    string ZoneDisplayName,
    IActorRef ZoneActor,
    string Location) {

    public static PvPPlayerProfile FromWizard(Wizard wizard, IActorRef zoneActor, string realmName) {
        return new PvPPlayerProfile(
            CharId: wizard.CharId,
            GameObjectId: wizard.GameObjectID,
            NameBlob: wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SchoolId: MagicSchoolIndex.ToSocialIndex(wizard.MagicSchoolBehavior.MagicSchool),
            Level: (uint) wizard.MagicSchoolBehavior.Level,
            RealmName: realmName ?? string.Empty,
            ZonePath: wizard.Zone ?? string.Empty,
            ZoneDisplayName: wizard.ZoneDisplayName ?? string.Empty,
            ZoneActor: zoneActor,
            Location: Util.GetCompactStringFromVector(wizard.Location, wizard.Orientation)
        );
    }

}
