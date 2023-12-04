/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandContext {
    public IActorRef SessionActor { get; init; }
    public TypeCache.CoreObject CharacterObject { get; init; }
    public Wizard Character { get; init; }
    public Account Account { get; init; }
    public IActorRef ZoneActor { get; init; }
    public IActorRef ServerActor { get; init; }
    public Wizard SelectedCharacter { get; init; }
    public Account SelectedAccount { get; init; }
}
