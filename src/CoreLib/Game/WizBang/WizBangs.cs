/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizBang;

internal class WizBangs : RootSingleResourceSingleton<WizBangs>, IMemoryStreamDisposable {
    protected override string ResourceName => "WizBangs.xml";

    private static readonly Dictionary<string, WizBangTemplate> s_templates = new();

    protected override void AfterLoad() {
        var serializer = new FileSerializer();

        var wizBangTemplates = serializer.OpenClass<WizBangTemplateManager>(base.Stream);
        if (wizBangTemplates is null) {
            Logger.Error("Could not deserialize {0} as {1}", Logger.Args(ResourceName, nameof(WizBangTemplateManager)));
            return;
        }

        foreach (var wizBangTemplate in wizBangTemplates.m_templates) {
            if (s_templates.ContainsKey(wizBangTemplate.m_name)) {
                Logger.Error("Duplicate WizBang name: {0}", Logger.Args(wizBangTemplate.m_name));
                continue;
            }

            s_templates.Add(wizBangTemplate.m_name, wizBangTemplate);
        }

        Logger.Information("Loaded {0} sigils.", Logger.Args(wizBangTemplates.m_templates.Count));
    }

    /// <summary>
    /// Tries to get the WizBang template with the specified name.
    /// </summary>
    /// <param name="wizBangName">The name of the WizBang.</param>
    /// <param name="wizBangTemplate">When this method returns, contains the WizBang template associated with the specified name, if found; otherwise, the default value.</param>
    /// <returns><c>true</c> if the WizBang template with the specified name is found; otherwise, <c>false</c>.</returns>
    internal static bool TryGetWizBangTemplate(string wizBangName, out WizBangTemplate wizBangTemplate)
        => s_templates.TryGetValue(wizBangName, out wizBangTemplate);

    /// <summary>
    /// Checks if a WizBang with the specified name exists.
    /// </summary>
    /// <param name="wizBangName">The name of the WizBang to check.</param>
    /// <returns><c>true</c> if a WizBang with the specified name exists; otherwise, <c>false</c>.</returns>
    internal static bool DoesWizBangExist(string wizBangName) => s_templates.ContainsKey(wizBangName);

    public void DisposeStream() => base.Stream.Dispose();
}
