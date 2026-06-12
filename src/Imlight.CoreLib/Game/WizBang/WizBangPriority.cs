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
 * WIZBANG PRIORITY MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and management of WizBang priority configurations
 * with methods to determine and sort WizBangs based on predefined priority rules.
 * Defined in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var highestPriorityWizBang = WizBangPriority.GetHighestPriorityWizBang(wizBangList);
 * var sortedWizBangs = WizBangPriority.GetPrioritySortedWizBangs(wizBangList);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.WizBang;

/// <summary>
/// Manages the loading and prioritization of WizBang templates.
/// </summary>
internal class WizBangPriority : RootSingleResourceSingleton<WizBangPriority>, IMemoryStreamDisposable {

    protected override string ResourceName => "WizBangPriority.xml";

    private static WizBangPriorityTemplate s_wizBangPriority;
    private static List<WizBangs> s_wizBangList = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_wizBangPriority)) {
            Logger.Error("Could not deserialize {0} as {1}", 
                Logger.Args(ResourceName, nameof(WizBangTemplateManager)));

            return;
        }

        // Convert the WizBangPriorityTemplate, which is a list of string, into a list of our WizBang enum.
        var priorityList = s_wizBangPriority.m_priorityList;
        var wizBangList = new List<WizBangs>(priorityList.Count);
        foreach (var wizBang in priorityList) {
            if (Enum.TryParse(wizBang, out WizBangs wizBangEnum)) {
                wizBangList.Add(wizBangEnum);
            } else {
                Logger.Error("Could not parse {0} as {1}", 
                    Logger.Args(wizBang, nameof(WizBangs)));
            }
        }

        s_wizBangList = wizBangList;

        Logger.Information("Loaded WizBang priority list with {0} entries",
            Logger.Args(s_wizBangPriority.m_priorityList.Count));
    }

    /// <summary>
    /// Gets the highest priority WizBang from the given list of WizBangs.
    /// </summary>
    /// <param name="wizBangs">The list of WizBangs to search.</param>
    /// <returns>The highest priority WizBang, or null if the WizBangPriority is null or no matching WizBang is found.</returns>
    internal static WizBangs GetHighestPriorityWizBang(List<WizBangs> wizBangs) {
        if (s_wizBangPriority is null) {
            Logger.Error("WizBangPriority is null");

            return WizBangs.None;
        }

        foreach (var wizBang in s_wizBangList) {
            if (wizBangs.Contains(wizBang)) {
                return wizBang;
            }
        }

        return WizBangs.None;
    }

    /// <summary>
    /// Gets a list of wiz bangs sorted by priority.
    /// </summary>
    /// <param name="wizBangs">The list of wiz bangs to sort.</param>
    /// <returns>A new list of wiz bangs sorted by priority.</returns>
    internal static List<WizBangs> GetPrioritySortedWizBangs(List<WizBangs> wizBangs) {
        if (s_wizBangPriority is null) {
            Logger.Error("WizBangPriority is null");

            return null;
        }

        var newList = new List<WizBangs>(wizBangs.Count);
        foreach (var wizBang in s_wizBangList) {
            if (wizBangs.Contains(wizBang)) {
                newList.Add(wizBang);
            }
        }

        return newList;
    }

    public void DisposeStream() 
        => base.Stream.Dispose();

}
