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
 * DIALOG SPEAKERS
 * ========================================================================
 *
 * PURPOSE:
 * Resolves the persona named by a dialog line to its NPC template, so a line shows
 * that NPC's name and portrait, and a quest is offered by the NPC who speaks it.
 *
 * USAGE EXAMPLE:
 * Serialize DialogSpeakers.ForClient(dialog) instead of the template's own dialog.
 *
 * NOTE:
 * Quest data names the speaker only by persona; live fills the name and portrait
 * when it sends the dialog. A persona is "<object name>_Persona" of the NPC whose
 * questing behavior carries it; the behavior is checked, so the name only finds candidates.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Madlibs;

internal static class DialogSpeakers {

    private const string PERSONA_NAME_SUFFIX = "_Persona";

    private static readonly ConcurrentDictionary<string, GameObjectTemplate> s_speakerByPersona = new();
    private static Dictionary<string, List<ulong>> s_templateIdsByObjectName;

    /// <summary>
    /// The dialog as the client must get it: a persona line that names no speaker gets the
    /// persona NPC's name and, when it has none, its portrait. The template is left untouched.
    /// </summary>
    public static ActorDialog ForClient(ActorDialog dialog) {
        if (dialog?.m_dialogEntries is null || !dialog.m_dialogEntries.Any(NamesNoSpeaker)) {
            return dialog;
        }

        return dialog with { m_dialogEntries = [.. dialog.m_dialogEntries.Select(WithSpeaker)] };
    }

    /// <summary>
    /// True when the NPC speaks the line: by persona when both have one, else by template id.
    /// </summary>
    public static bool IsSpokenBy(ActorDialogEntry entry, CoreTemplate npcTemplate) {
        if (entry is null || npcTemplate is not GameObjectTemplate npc) {
            return false;
        }

        var npcPersona = PersonaOf(npc);
        if (!string.IsNullOrEmpty(npcPersona) && entry is NPCDialogEntry { m_personaName: { Length: > 0 } personaName }) {
            return npcPersona == personaName;
        }

        return entry.m_actorTemplateID == npc.m_templateID;
    }

    private static bool NamesNoSpeaker(ActorDialogEntry entry)
        => entry is NPCDialogEntry npcEntry
           && !string.IsNullOrEmpty(npcEntry.m_personaName)
           && string.IsNullOrEmpty(npcEntry.m_nameSTKey)
           && string.IsNullOrEmpty(npcEntry.m_nameOverride);

    private static ActorDialogEntry WithSpeaker(ActorDialogEntry entry) {
        if (!NamesNoSpeaker(entry)) {
            return entry;
        }

        var npcEntry = (NPCDialogEntry) entry;
        var speaker = FindSpeaker(npcEntry.m_personaName);

        if (speaker is null) {
            return entry;
        }

        return npcEntry with {
            m_nameOverride = speaker.m_displayName,
            m_picture = string.IsNullOrEmpty(npcEntry.m_picture) ? speaker.m_sIcon : npcEntry.m_picture,
        };
    }

    private static GameObjectTemplate FindSpeaker(string personaName)
        => s_speakerByPersona.GetOrAdd(personaName, ResolveSpeaker);

    private static GameObjectTemplate ResolveSpeaker(string personaName) {
        if (!personaName.EndsWith(PERSONA_NAME_SUFFIX, StringComparison.Ordinal)) {
            return null;
        }

        var objectName = personaName[..^PERSONA_NAME_SUFFIX.Length];
        if (!TemplateIdsByObjectName().TryGetValue(objectName, out var templateIds)) {
            return null;
        }

        return templateIds
            .Select(id => CoreObjectFactory.GetCoreTemplate(id) as GameObjectTemplate)
            .FirstOrDefault(template => template is not null && PersonaOf(template) == personaName);
    }

    private static string PersonaOf(GameObjectTemplate npc)
        => npc.m_behaviors?.OfType<WizardQuestingBehaviorTemplate>().FirstOrDefault()?.m_personaName;

    private static Dictionary<string, List<ulong>> TemplateIdsByObjectName()
        => LazyInitializer.EnsureInitialized(ref s_templateIdsByObjectName, IndexTemplateIdsByObjectName);

    private static Dictionary<string, List<ulong>> IndexTemplateIdsByObjectName() {
        var index = new Dictionary<string, List<ulong>>(StringComparer.Ordinal);
        foreach (var location in CoreObjectFactory.TemplateManifest.m_serializedTemplates) {
            if (location?.m_filename is null) {
                continue;
            }

            var objectName = Path.GetFileNameWithoutExtension(location.m_filename);
            if (!index.TryGetValue(objectName, out var templateIds)) {
                templateIds = [];
                index[objectName] = templateIds;
            }

            templateIds.Add(location.m_id);
        }

        return index;
    }

}
