/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Util.Internal;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandSpellbookProtocol : CommandProtocol {
    internal override string Group { get; set; } = "deck";

    [Command("learn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void LearnSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        var spell = SpellFactory.CreateSpellFromTemplate(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        if (!Context.Character.LearnSpell(spell)) {
            InformSenderClient("Failed to learn spell. You may already know this spell.");
        }
        else {
            InformSenderClient("You have learned the spell.");
        }
    }

    [Command("unlearn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void UnlearnSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        var spell = SpellFactory.CreateSpellFromTemplate(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        if (!Context.Character.UnlearnSpell(spellTemplateIdUint)) {
            InformSenderClient("Failed to unlearn spell. You may not know this spell.");
        }
        else {
            InformSenderClient("You have unlearned the spell.");
        }
    }

    [Command("add")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void AddSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        var spell = SpellFactory.CreateSpellFromTemplate(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        Context.Character.AddSpell(spell);
        InformSenderClient("You have added the spell.");
    }

    [Command("remove")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void RemoveSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        var spell = SpellFactory.CreateSpellFromTemplate(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");
            return;
        }

        Context.Character.RemoveSpell(spellTemplateIdUint);
        InformSenderClient("You have removed the spell.");
    }
}
