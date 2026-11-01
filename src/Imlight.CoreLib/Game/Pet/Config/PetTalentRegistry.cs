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
 * PET TALENT REGISTRY
 * ========================================================================
 *
 * PURPOSE:
 * Loads every pet talent template (combat talents, derby talents and pet powers) from the root
 * archive and indexes them by exact talent name and by template id.
 *
 * USAGE EXAMPLE:
 * if (PetTalentRegistry.TryGetByName(poolEntry, out var talent)) { geneIds.Add(talent.Id); }
 *
 * NOTE:
 * The files carry no template id. The client keys a talent by the string hash of its name, which is
 * the id that travels in the pet's expressed talent lists.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using System;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Pet.Config;

/// <summary>
/// What a pet talent template does.
/// </summary>
internal enum PetTalentKind {

    Combat,
    Derby,
    Power,

}

/// <summary>
/// A talent template with the id and kind the registry gives it.
/// </summary>
/// <param name="Id">The template id: the string hash of the talent name.</param>
/// <param name="Name">The exact talent name.</param>
/// <param name="Kind">Whether the template is a combat talent, a derby talent or a pet power.</param>
/// <param name="Template">The template as read from the root archive.</param>
internal sealed record PetTalentInfo(uint Id, string Name, PetTalentKind Kind, PetTalentBaseTemplate Template) {

    /// <summary>
    /// The talent's rank, which is what the pet ratings add up.
    /// </summary>
    internal int Rank => Template.m_rank;

}

/// <summary>
/// Every pet talent template of the root archive, from TalentData and TalentData/PetPowers.
/// </summary>
internal sealed class PetTalentRegistry : RootDirectoryResourceSingleton<PetTalentRegistry> {

    private const string POWER_DIRECTORY = "TalentData/PetPowers/";

    protected override string DirectoryName => "TalentData/";

    private static Dictionary<string, PetTalentInfo> s_byName = new(StringComparer.Ordinal);
    private static Dictionary<uint, PetTalentInfo> s_byId = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var byName = new Dictionary<string, PetTalentInfo>(StringComparer.Ordinal);
        var byId = new Dictionary<uint, PetTalentInfo>();

        foreach (var (fileRecord, fileStream) in Files) {
            if (!fileRecord.FileName.EndsWith(".xml", StringComparison.Ordinal)) {
                continue;
            }

            if (!serializer.Deserialize<PetTalentBaseTemplate>(fileStream?.ToArray(), out var template)) {
                Logger.Error("Could not deserialize {File} as {Type}", 
                    Logger.Args(fileRecord.FileName, nameof(PetTalentBaseTemplate)));

                continue;
            }

            var name = template.m_talentName.ToString();
            var kind = template switch {
                PetDerbyTalentTemplate => PetTalentKind.Derby,
                _ when fileRecord.FileName.StartsWith(POWER_DIRECTORY, StringComparison.Ordinal) => PetTalentKind.Power,
                _ => PetTalentKind.Combat
            };
            var info = new PetTalentInfo(StringHash.Compute(name), name, kind, template);

            if (!byName.TryAdd(name, info)) {
                Logger.Error("{File} repeats the talent name {Talent}.", 
                    Logger.Args(fileRecord.FileName, name));

                continue;
            }

            if (!byId.TryAdd(info.Id, info)) {
                Logger.Error("The talent {Talent} has the same id as {Other}.", 
                    Logger.Args(name, byId[info.Id].Name));
                byName.Remove(name);
            }
        }

        s_byName = byName;
        s_byId = byId;
        Logger.Information("Loaded {Count} pet talent templates.", 
            Logger.Args(byName.Count));
    }

    /// <summary>
    /// Every talent template, in no particular order.
    /// </summary>
    internal static IReadOnlyCollection<PetTalentInfo> All => s_byName.Values;

    /// <summary>
    /// Finds a talent by its exact name.
    /// </summary>
    /// <param name="name">The talent name, as a pet template lists it.</param>
    /// <param name="talent">The talent, if found.</param>
    /// <returns>True if a talent has exactly this name.</returns>
    internal static bool TryGetByName(string name, out PetTalentInfo talent) {
        talent = null;

        return name is not null && s_byName.TryGetValue(name, out talent);
    }

    /// <summary>
    /// Finds a talent by the id a pet carries in its talent lists.
    /// </summary>
    /// <param name="id">The template id.</param>
    /// <param name="talent">The talent, if found.</param>
    /// <returns>True if a talent has this id.</returns>
    internal static bool TryGetById(uint id, out PetTalentInfo talent)
        => s_byId.TryGetValue(id, out talent);

}
