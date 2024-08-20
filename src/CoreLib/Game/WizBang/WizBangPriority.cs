/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Resources;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.WizBang;

internal class WizBangPriority : RootSingleResourceSingleton<WizBangs>, IMemoryStreamDisposable{
    protected override string ResourceName => "WizBangPriority.xml";

    private static WizBangPriorityTemplate s_wizBangPriority;

    protected override void AfterLoad() {
        var serializer = new FileSerializer();

        s_wizBangPriority = serializer.OpenClass<WizBangPriorityTemplate>(base.Stream);
        if (s_wizBangPriority is null) {
            Logger.Error("Could not deserialize {0} as {1}", Logger.Args(ResourceName, nameof(WizBangTemplateManager)));
            return;
        }

        Logger.Information("Loaded WizBang priority list with {0} entries",
            Logger.Args(s_wizBangPriority.m_priorityList.Count));
    }

    /// <summary>
    /// Gets the highest priority WizBang from the given list of WizBangs.
    /// </summary>
    /// <param name="wizBangs">The list of WizBangs to search.</param>
    /// <returns>The highest priority WizBang, or null if the WizBangPriority is null or no matching WizBang is found.</returns>
    public static string GetHighestPriorityWizBang(List<string> wizBangs) {
        if (s_wizBangPriority is null) {
            Logger.Error("WizBangPriority is null");
            return null;
        }

        foreach (var wizBang in s_wizBangPriority.m_priorityList) {
            if (wizBangs.Contains(wizBang)) {
                return wizBang;
            }
        }

        return null;
    }

    public static List<string> GetPrioritySortedWizBangs(List<string> wizBangs) {
        if (s_wizBangPriority is null) {
            Logger.Error("WizBangPriority is null");
            return null;
        }

        return s_wizBangPriority.m_priorityList.Where(wizBangs.Contains);
    }

    public void DisposeStream() => base.Stream.Dispose();
}
