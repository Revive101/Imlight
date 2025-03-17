/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandSpellbookProtocol : CommandProtocol {

    internal override string Group { get; set; } = "sb";

    [Command("learn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void LearnSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        var template = CoreObjectFactory.GetCoreTemplate(spellTemplateIdUint);
        if (template == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }
        if (template is not SpellTemplate spellTemplate) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }
        var spellName = spellTemplate.m_name;

        var spell = SpellFactory.GetSpell(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        if (!Context.Character.LearnSpell(spell)) {
            InformSenderClient("Failed to learn spell. You may already know this spell.");
        }
        else {
            InformSenderClient($"You have learned the spell {spellName}.");
        }

        var clientMsg = new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK {
            SpellID = (int) spellTemplateIdUint
        };
        Context.SessionActor.Tell(clientMsg);
    }

    [Command("unlearn")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void UnlearnSpellCommand(string spellTemplateId) {
        // Parse the spell template ID as a uint.
        if (!uint.TryParse(spellTemplateId, out var spellTemplateIdUint)) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        var template = CoreObjectFactory.GetCoreTemplate(spellTemplateIdUint);
        if (template == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }
        if (template is not SpellTemplate spellTemplate) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }
        var spellName = spellTemplate.m_name;

        var spell = SpellFactory.GetSpell(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        if (!Context.Character.UnlearnSpell(spellTemplateIdUint)) {
            InformSenderClient("Failed to unlearn spell. You may not know this spell.");
        }
        else {
            InformSenderClient($"You have unlearned the spell {spellName}.");
        }

        var clientMsg = new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMBOOK {
            SpellID = (int) spellTemplateIdUint
        };
        Context.SessionActor.Tell(clientMsg);
    }
    [Command("unlearnall")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void UnlearnallSpellCommand() {

        if (Context.Character.SpellbookBehavior.LearnedSpellTemplateIds != null) {
            List<uint> spellList = [.. Context.Character.SpellbookBehavior.LearnedSpellTemplateIds];

            foreach (var spell in spellList) {
                var clientMsg = new WIZARD_12_PROTOCOL.MSG_REMOVESPELLFROMBOOK {
                    SpellID = (int) spell
                };
                Context.SessionActor.Tell(clientMsg);

                Context.Character.SpellbookBehavior.RemoveSpellFromBook(spell);

                // Persistent save.
                WizardCollection.UnlearnSpell(Context.Character, spell);
                InformSenderClient($"You have unlearned the spell {spell}.");
            }
        }
        else {
            InformSenderClient($"No Spells To Unlearn");
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

        var spell = SpellFactory.GetSpell(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        Context.Character.AddTemporarySpell(spell);
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

        var spell = SpellFactory.GetSpell(spellTemplateIdUint);
        if (spell == null) {
            InformSenderClient("Invalid spell template ID.");

            return;
        }

        Context.Character.RemoveTemporarySpell(spellTemplateIdUint);
        InformSenderClient("You have removed the spell.");
    }

}
