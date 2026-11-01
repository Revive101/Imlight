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
 * PET TALENT TRIGGERED SPELLS
 * ========================================================================
 *
 * PURPOSE:
 * Loads which spells each pet combat talent can trigger from the root archive.
 *
 * USAGE EXAMPLE:
 * foreach (var spellName in PetTalentsTriggeredSpells.GetSpellNames(talentName)) { ... }
 *
 * NOTE:
 * Talents are keyed by their TalentData name, spells by their spell template name.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System.Linq;
using System;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// The spells each pet combat talent can trigger, as defined in PetTalentsTriggeredSpells.xml.
/// </summary>
internal sealed class PetTalentsTriggeredSpells : RootSingleResourceSingleton<PetTalentsTriggeredSpells>, IMemoryStreamDisposable {

    protected override string ResourceName => "PetTalentsTriggeredSpells.xml";

    private static Dictionary<string, IReadOnlyList<string>> s_spellsByTalent = new(StringComparer.Ordinal);

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize<Imcodec.ObjectProperty.TypeCache.PetTalentsTriggeredSpells>(Stream.ToArray(), out var config)) {
            Logger.Error("Failed to deserialize PetTalentsTriggeredSpells.xml");

            return;
        }

        var byTalent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var entry in config.m_petTalentsTriggeredSpells) {
            var talentName = entry.m_talentName.ToString();
            if (!byTalent.TryAdd(talentName, [.. entry.m_triggeredSpells.Select(spell => spell.ToString())])) {
                Logger.Error("PetTalentsTriggeredSpells.xml lists the talent {Talent} twice.", Logger.Args(talentName));
            }
        }

        s_spellsByTalent = byTalent;
        Logger.Information("Loaded the triggered spells of {Count} pet talents.", Logger.Args(byTalent.Count));

        DisposeStream();
    }

    /// <summary>
    /// The names of every talent that triggers a spell.
    /// </summary>
    internal static IReadOnlyCollection<string> TalentNames => s_spellsByTalent.Keys;

    /// <summary>
    /// Gets the spells a talent can trigger.
    /// </summary>
    /// <param name="talentName">The talent's TalentData name.</param>
    /// <returns>The spell template names, empty if the talent triggers none.</returns>
    internal static IReadOnlyList<string> GetSpellNames(string talentName)
        => talentName is not null && s_spellsByTalent.TryGetValue(talentName, out var spells) ? spells : [];

    public void DisposeStream()
        => base.Stream.Dispose();

}
