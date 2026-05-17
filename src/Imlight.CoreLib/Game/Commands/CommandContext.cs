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

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandContext {

    public IActorRef SessionActor { get; init; }
    public CoreObject CharacterObject { get; init; }
    public Wizard Character { get; init; }
    public Account Account { get; init; }
    public IActorRef ZoneActor { get; init; }
    public IActorRef ServerActor { get; init; }
    public Wizard SelectedCharacter { get; init; }
    public Account SelectedAccount { get; init; }

}
