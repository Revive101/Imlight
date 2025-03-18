/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Spells;

public class SpellFactory : RootDirectoryResourceSingleton<SpellFactory>, IMemoryStreamDisposable {

    protected override string DirectoryName => "Spells/";

    private static readonly Dictionary<uint, SpellTemplate> s_spellTemplates = new();
    private static readonly Dictionary<uint, string> s_spellTemplatePaths = new();

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var counter = 0;

        foreach (var file in base.Files) {
            var fileRecord = file.Key;
            var fileStream = file.Value;

            if (!serializer.Deserialize<SpellTemplate>(fileStream.ToArray(), 1, out var spellTemplate)) {
                Logger.Error("Could not deserialize {0} as {1}", 
                    Logger.Args(fileRecord.FileName, nameof(SpellTemplate)));

                continue;
            }

            var stringHash = StringHash.Compute(spellTemplate.m_name);

            s_spellTemplates.Add(stringHash, spellTemplate);
            counter++;

            // Store the path of the spell template for later use.
            s_spellTemplatePaths.Add(stringHash, fileRecord.FileName);
        }

        Logger.Information("Loaded {0} spell templates.", Logger.Args(counter));
    }

    /// <summary>
    /// Retrieves a spell by its name.
    /// </summary>
    /// <param name="spellName">The name of the spell.</param>
    /// <returns>The spell with the specified name, or null if the spell template is not found.</returns>
    public static Spell GetSpell(string spellName) {
        var stringHash = StringHash.Compute(spellName);
        if (!s_spellTemplates.TryGetValue(stringHash, out var spellTemplate)) {
            Logger.Warning("Could not find spell template with name {0}.", Logger.Args(spellName));
            return null;
        }

        var spellTemplateActualPath = s_spellTemplatePaths[stringHash];
        var templateId = CoreObjectFactory.GetCoreTemplateID(x => x.m_filename == spellTemplateActualPath);

        if (templateId == 0) {
            Logger.Warning("Could not find template ID for spell {0}.", Logger.Args(spellName));
            return null;
        }

        return GetSpell(spellTemplate, templateId);
    }

    /// <summary>
    /// Creates a spell from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the spell template.</param>
    /// <returns>The created spell object.</returns>
    public static Spell GetSpell(uint templateId) {
        var template = CoreObjectFactory.GetCoreTemplate(templateId);

        if (template is null) {
            Logger.Warning("Could not find spell template with ID {0}.", Logger.Args(templateId));
            
            return null;
        }

        if (template is not SpellTemplate spellTemplate) {
            Logger.Warning("Template with ID {0} is not a spell template.", Logger.Args(templateId));
            
            return null;
        }

        return GetSpell(spellTemplate, templateId);
    }

    /// <summary>
    /// Creates a spell from a spell template.
    /// </summary>
    /// <param name="template">The spell template to create the spell from.</param>
    /// <returns>The created spell object.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static Spell GetSpell(SpellTemplate template, uint templateId) {
        if (template is null) {
            throw new ArgumentNullException(nameof(template));
        }

        // Hash the spell name to create a unique spell ID.
        var spellId = StringHash.Compute(template.m_name);

        var spell = new Spell() {
            m_templateID = templateId,
            m_pipCost = template.m_spellRank,
            m_accuracy = (byte) template.m_accuracy,
            m_treasureCard = template.m_Treasure,
            m_spellID = spellId,
            m_itemCard = false, // todo: bad
            m_magicSchoolID = StringHash.Compute(template.m_sMagicSchoolName),
        };

        return spell;
    }

    /// <summary>
    /// Creates an array of spells based on the provided spell effect.
    /// </summary>
    /// <param name="effect">The spell effect used to create the spells.</param>
    /// <returns>An array of spells created from the spell effect.</returns>
    public static Spell[] CreateSpellsFromEffect(ProvideSpellEffect effect) {
        var spell = GetSpell(effect.m_spellName);
        if (spell is null) {
            Logger.Warning("Could not create spell from effect {0}.", Logger.Args(effect.m_spellName));

            return null;
        }

        var spells = new List<Spell>();
        for (var i = 0; i < effect.m_numSpells; i++) {
            // Indicate to the client that this spell is given by an item.
            spell.m_itemCard = true;
            spells.Add(spell);
        }

        return spells.ToArray();
    }

    /// <summary>
    /// Retrieves the base spell name for a given template ID.
    /// </summary>
    /// <param name="templateId">The ID of the spell template.</param>
    /// <returns>The base spell name, or null if the template is not found.</returns>
    public static string GetBaseSpellName(uint templateId) {
        var template = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        return template?.m_spellBase;
    }

    public void DisposeStream() {
        s_spellTemplates.Clear();
        s_spellTemplatePaths.Clear();
    }
    
}
