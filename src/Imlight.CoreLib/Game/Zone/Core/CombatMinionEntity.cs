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
 * COMBAT MINION ENTITY
 * ========================================================================
 * 
 * PURPOSE:
 * A creature summoned into a duel by a kSummonCreature spell effect (a minion).
 * 
 * USAGE EXAMPLE:
 * Created by CombatDuelComponent.SummonMinion via ZoneEntity.SpawnCombatMinionActor.
 * 
 * NOTE:
 * A real ZoneEntity (creature combat brain included) that stays in its player-team
 * sub-circle: IsCombatOnlyMinion suppresses PathMovement and the distance-cull.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 08/12/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// A creature summoned into a duel by a <c>kSummonCreature</c> spell effect (a minion).
/// IsCombatOnlyMinion suppresses PathMovement and the distance-cull so it stays in its slot.
/// </summary>
internal sealed class CombatMinionEntity(
    CoreObject activeGameObject,
    CoreTemplate template,
    CoreObjectInfo info,
    IActorRef zoneRef,
    Zone zone)
    : ZoneEntity(activeGameObject, template, info, zoneRef, zone) {

    public override bool IsCombatOnlyMinion => true;

}
