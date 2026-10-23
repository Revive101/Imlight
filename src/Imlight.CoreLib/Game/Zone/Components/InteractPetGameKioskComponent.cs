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
 * INTERACT PET GAME KIOSK COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Offers the pet game kiosks and opens the client's pet game window for the wizard's equipped pet.
 *
 * USAGE EXAMPLE:
 * Automatically attached to objects that carry a PetGameKioskBehaviorTemplate.
 *
 * NOTE:
 * The kiosk's game comes from the data: the object template's icon is joined to the icon of a game in
 * PetGames.xml. Failing that, the object name is matched against the game names. A kiosk that matches
 * no game sends an empty game info, which gives the client's full game menu.
 * Closing the window sends MSG_DONESHOPPING, which ShopService and TreasureShopService already answer
 * (both only restore movement and clear the wizbang), so the kiosk needs no handler of its own.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using PetGameConfig = Imlight.CoreLib.Game.Pet.Config.PetGameConfig;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractPetGameKioskComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string SERVICE_NAME = "PetGameKioskService";
    private const string OPTION_ICON_KEY = "Kiosk";
    private const string OPTION_DISPLAY_KEY = "GUI_PlayMini";
    private const string NO_EQUIPPED_PET_TEXT = "You need to equip a pet to play pet games.";
    private const int NO_EQUIPPED_PET_NOTIFY_TYPE = 0;
    private const uint PET_GAME_INFO_PROPERTY_MASK = 7;

    public string ServiceName => SERVICE_NAME;
    public string NpcIcon => "";
    public string NpcNameKey => null;
    public string NpcTextKey => OPTION_DISPLAY_KEY;
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => OPTION_DISPLAY_KEY;

    private PetGameInfo _game;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate
        && template.m_behaviors.Exists(behavior => behavior is PetGameKioskBehaviorTemplate);

    public override void OnStart() {
        _game = FindGame(Entity.Template as GameObjectTemplate);
        if (_game is null) {
            Logger.Warning("The pet game kiosk {0} matches no pet game, so it opens the full game menu.",
                Logger.Args(Entity.ActiveGameObject.m_debugName));
        }
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter)
        => [
            new PetGameKioskOption {
                m_serviceName = SERVICE_NAME,
                m_iconKey = OPTION_ICON_KEY,
                m_displayKey = OPTION_DISPLAY_KEY,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var pet = playerCharacter.GetEquippedPetItem();
        if (pet is null) {
            playerActor.Tell(new GAME_5_PROTOCOL.MSG_CLIENTNOTIFYTEXT {
                NotifyText = NO_EQUIPPED_PET_TEXT,
                Type = NO_EQUIPPED_PET_NOTIFY_TYPE
            });

            return;
        }

        playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_PETGAMEKIOSK {
            GlobalID = Entity.ActiveGameObject.m_globalID.Full,
            GameInfo = SerializeGame(_game),
            GamesWon = _game is null ? 0 : WizardPetCollection.GetGamesWon(playerCharacter.CharId, pet.m_globalID.Full, _game.m_name.ToString())
        });
    }

    private static PetGameInfo FindGame(GameObjectTemplate template) {
        if (template is null) {
            return null;
        }

        var icon = template.m_sIcon.ToString();
        var objectName = template.m_objectName.ToString();
        PetGameInfo byName = null;
        foreach (var game in PetGameConfig.Games) {
            var gameIcon = game.m_gameIcon.ToString();
            if (gameIcon.Length > 0 && string.Equals(gameIcon, icon, StringComparison.OrdinalIgnoreCase)) {
                return game;
            }

            var gameName = game.m_name.ToString();
            var isNameMatch = gameName.Length > 0 && objectName.StartsWith(gameName, StringComparison.Ordinal);
            if (isNameMatch && (byName is null || gameName.Length > byName.m_name.ToString().Length)) {
                byName = game;
            }
        }

        return byName;
    }

    private static ByteString SerializeGame(PetGameInfo game) {
        if (game is null) {
            return default;
        }

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(game, PET_GAME_INFO_PROPERTY_MASK, out var data)) {
            Logger.Error("Failed to serialize the pet game {0}.", Logger.Args(game.m_name.ToString()));

            return default;
        }

        return data;
    }

}
