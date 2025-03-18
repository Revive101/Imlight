/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.WizBang;

internal class WizBangPriority : RootSingleResourceSingleton<WizBangs>, IMemoryStreamDisposable {

    protected override string ResourceName => "WizBangPriority.xml";

    private static WizBangPriorityTemplate s_wizBangPriority;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize<WizBangPriorityTemplate>(base.Stream.ToArray(), 1, out s_wizBangPriority)) {
            Logger.Error("Could not deserialize {0} as {1}", 
                Logger.Args(ResourceName, nameof(WizBangTemplateManager)));

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

    /// <summary>
    /// Gets a list of wiz bangs sorted by priority.
    /// </summary>
    /// <param name="wizBangs">The list of wiz bangs to sort.</param>
    /// <returns>A new list of wiz bangs sorted by priority.</returns>
    public static List<string> GetPrioritySortedWizBangs(List<string> wizBangs) {
        if (s_wizBangPriority is null) {
            Logger.Error("WizBangPriority is null");
            return null;
        }

        var newList = new List<string>();
        foreach (var wizBang in s_wizBangPriority.m_priorityList) {
            if (wizBangs.Contains(wizBang)) {
                newList.Add(wizBang);
            }
        }

        return newList;
    }

    public void DisposeStream() => base.Stream.Dispose();

}
