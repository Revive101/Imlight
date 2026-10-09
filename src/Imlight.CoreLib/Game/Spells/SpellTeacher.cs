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
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Spells;

/// <summary>
/// The outcome of <see cref="SpellTeacher.TryTeach"/>.
/// </summary>
internal enum SpellTeachResult {
    Learned,
    AlreadyKnown,
    Failed,
}

/// <summary>
/// Teaches a spell to a wizard and informs the game client. Shared by the ResLearnSpell
/// result handler and the loot granter so both use the same learn path.
/// </summary>
internal static class SpellTeacher {

    /// <summary>
    /// Teaches the spell with the given template ID to the wizard and pushes it to the client's spellbook.
    /// </summary>
    /// <param name="playerActor">The player's SessionActor, which routes the message.</param>
    /// <param name="wizard">The wizard to teach.</param>
    /// <param name="spellTemplateId">The spell template ID.</param>
    /// <returns>Learned, AlreadyKnown (nothing changed), or Failed (no spell for that template).</returns>
    internal static SpellTeachResult TryTeach(IActorRef playerActor, Wizard wizard, uint spellTemplateId) {
        var spell = SpellFactory.GetSpell(spellTemplateId);
        if (spell is null) {
            Logger.Error("Could not resolve a spell for template ID {0}.",
                Logger.Args(spellTemplateId));

            return SpellTeachResult.Failed;
        }

        if (wizard.SpellbookBehavior.HasSpell(spell.m_templateID)) {
            // Already known; teaching is idempotent.
            return SpellTeachResult.AlreadyKnown;
        }

        if (!wizard.LearnSpell(spell)) {
            return SpellTeachResult.AlreadyKnown;
        }

        // The attach payload with the spellbook was already sent, so push the new spell to the client.
        playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK {
            SpellID = (int) spellTemplateId,
        });

        return SpellTeachResult.Learned;
    }

}
