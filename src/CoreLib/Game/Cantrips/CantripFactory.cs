/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Spells;

public static class CantripFactory {
    /// <summary>
    /// Creates a cantrip template from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the cantrip template.</param>
    /// <returns>The created cantrip template object.</returns>
    public static CantripsSpellTemplate CreateCantripTemplateFromId(uint templateId) {
        var template = (CantripsSpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        if (template == null) {
            Logger.Warning("Tried to create cantrip from non-existent template {0}.", Logger.Args(templateId));
            return null;
        }
        if (template is not CantripsSpellTemplate cantripSpellTemplate) {
            Logger.Warning("Tried to create cantrip from non-cantrip template {0}.", Logger.Args(templateId));
            return null;
        }

        return cantripSpellTemplate;
    }
}
