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

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Statues;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class StatueComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent {

    private const string LINE_BREAK_TAG = "<br>";
    private const string PARAGRAPH_BREAK_TAGS = "<br><br>";

    public string ServiceName     => "Interact";
    public string NpcIcon         => (Entity.Template as GameObjectTemplate)?.m_sIcon;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.None;
    public string StateName       => null;
    public string InteractWizBang => null;
    public string DisplayKey      => null;

    private PlayerStatue Statue => ((StatueEntity) Entity).Statue;

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
        if (string.IsNullOrWhiteSpace(Statue.PlaqueText)) {
            return [];
        }

        return [
            new InteractableOption { m_serviceName = ServiceName }
        ];
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (string.IsNullOrWhiteSpace(Statue.PlaqueText)) {
            return;
        }

        var text = Statue.PlaqueText.Replace("\r\n", "\n").Replace("\n", LINE_BREAK_TAG);
        playerActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE {
            Message = Statue.PlaqueTitle + PARAGRAPH_BREAK_TAGS + text,
            Modal = 1
        });
    }

}
