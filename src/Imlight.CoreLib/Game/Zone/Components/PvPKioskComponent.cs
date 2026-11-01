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
 * PVP KIOSK
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the league kiosks of the PvP lobby: the object offers one service option, and using it opens the
 * league window of the kiosk.
 * 
 * USAGE EXAMPLE:
 * Attached by the component registry to an object whose template has a PvP5thAgeKioskBehavior.
 * 
 * NOTE:
 * The league and the kiosk title come from the kiosk template. The window finds the kiosk object by its global
 * id, so KioskGID is the id of this entity. The player session is told which league the kiosk belongs to, and
 * only then accepts the request to open the PvP window of that league.
 * 
 * TODO:
 * Which service option class did the original kiosks use?
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class PvPKioskComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const float INTERACTION_TOLERANCE = 2.0f;

    public string ServiceName     => "PvPKioskService";
    public string NpcIcon         => null;
    public string NpcNameKey      => Entity.Template is GameObjectTemplate template ? (string) template.m_displayName : null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.None;
    public string StateName       => null;
    public string InteractWizBang => null;
    public string DisplayKey      => "GUI2_00001602";

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate gameObjectTemplate
        && gameObjectTemplate.m_behaviors.Any(x => x is PvP5thAgeKioskBehaviorTemplate);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter)
        => [
            new KioskOption {
                m_displayKey = DisplayKey,
                m_iconKey = "Kiosk",
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (!PvPSettings.IsEnabled) {
            playerActor.Tell(new WIZARD3_56_PROTOCOL.MSG_PVPDISABLED());

            return;
        }

        if (!IsInHorizontalRadius(playerObject, ((IServiceComponent) this).DEFAULT_INTERACTION_RADIUS * INTERACTION_TOLERANCE)) {
            Logger.Warning("{CharId} used the PvP kiosk {Kiosk} from too far away.",
                Logger.Args(playerCharacter.CharId, Entity.ActiveGameObject.m_debugName));

            return;
        }

        var behavior = (Entity.Template as GameObjectTemplate)?.m_behaviors
            .OfType<PvP5thAgeKioskBehaviorTemplate>()
            .FirstOrDefault();
        if (behavior is null || !PvPLeagueFactory.TryGetLeague(behavior.m_leagueType.ToString(), out var league)) {
            Logger.Error("The PvP kiosk {Kiosk} names a league that does not exist.", Logger.Args(Entity.ActiveGameObject.m_debugName));

            return;
        }

        if (!PvPSeasonClock.IsLeagueOpen(league)) {
            playerActor.Tell(PvPArenaErrors.Message(PvPArenaErrors.PLAYER_INELIGIBLE));

            return;
        }

        if (!PvPEligibility.MeetsLeagueRequirements(league, playerActor, playerObject, playerCharacter)) {
            playerActor.Tell(PvPArenaErrors.Message(PvPArenaErrors.NOT_IN_LEVEL_RANGE));

            return;
        }

        playerActor.Tell(new PVP_111_PROTOCOL.MSG_KIOSKUSED { LeagueId = StringHash.Compute(league.m_leagueType) });
        playerActor.Tell(new WIZARD3_56_PROTOCOL.MSG_PVP5THAGEKIOSKREQUEST {
            PlayerGID = playerObject.m_globalID.Full,
            Data = PvPBlobs.EmptyProjectProgress(),
            ProjectData = PvPBlobs.EmptyProjectActive(),
            KioskGID = Entity.ActiveGameObject.m_globalID.Full
        });
    }

}
