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
 * INTERACT SIGIL COMPONENT BASE
 * ========================================================================
 *
 * PURPOSE:
 * The press-X pad behavior that dungeon sigils and minigame sigils share: the service stubs, the
 * pad radius, the single Interact option, the lit state for each joining player and the pad location.
 *
 * USAGE EXAMPLE:
 * internal sealed class InteractDungeonSigilComponent(ZoneEntity entity) : InteractSigilComponentBase(entity)
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal abstract class InteractSigilComponentBase(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent {

    protected const string LIT_STATE = "Glowy";

    private const float DEFAULT_PAD_RADIUS = 370.0f;

    public string ServiceName => "Interact";
    public string NpcIcon => "";
    public string NpcNameKey => "";
    public string NpcTextKey => "";
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    public float DEFAULT_INTERACTION_RADIUS
        => SigilInfo is not null && SigilInfo.m_radius > 0 ? SigilInfo.m_radius : DEFAULT_PAD_RADIUS;

    protected MinigameSigilInfo SigilInfo
        => Entity.Info as MinigameSigilInfo;

    protected abstract bool IsAvailable { get; }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _) {
        if (!IsAvailable) {
            return [];
        }

        return [
            new InteractableOption { m_serviceName = ServiceName }
        ];
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (IsAvailable) {
            Entity.ChangeStateExclusiveSender(LIT_STATE, playerActor);
        }
    }

    public abstract void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex);

    protected string GetPadLocation() {
        var pad = Entity.ActiveGameObject;
        var location = pad.m_location;

        return Util.GetCompactStringFromVector(new Vector4(location.X, location.Y, location.Z, pad.m_orientation.Z));
    }

}
