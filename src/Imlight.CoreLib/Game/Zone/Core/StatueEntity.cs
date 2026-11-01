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
 * STATUE ENTITY
 * ========================================================================
 *
 * PURPOSE:
 * A server-spawned statue: an NPC-shaped zone object that looks like a captured wizard.
 *
 * USAGE EXAMPLE:
 * Created by ZoneStatueSupervisor for each enabled statue definition of a zone.
 *
 * NOTE:
 * Only the render, interact and plaque components attach, so a statue has no quest, vendor, combat or path
 * behavior, whatever its base template is.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Statues;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.CoreLib.Game.Zone.Components;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// A statue: an NPC-shaped zone object dressed as a captured wizard.
/// </summary>
internal sealed class StatueEntity(
    CoreObject activeGameObject,
    CoreTemplate template,
    CoreObjectInfo info,
    IActorRef zoneRef,
    Zone zone,
    PlayerStatue statue)
    : ZoneEntity(activeGameObject, template, info, zoneRef, zone) {

    public PlayerStatue Statue { get; } = statue;

    public override ClientObject GetClientObject() {
        var clientObject = base.GetClientObject();
        StatueLook.Apply(clientObject, Statue);

        return clientObject;
    }

    public override string GetMadlibName() => Statue.Name;

    protected override void AutoAttachComponents() {
        AddComponent(typeof(RenderComponent));
        AddComponent(typeof(InteractServiceMementoComponent));
        AddComponent(typeof(StatueComponent));
    }

}
