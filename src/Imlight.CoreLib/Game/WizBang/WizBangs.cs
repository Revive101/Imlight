/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.WizBang;

internal class WizBangs : RootSingleResourceSingleton<WizBangs>, IMemoryStreamDisposable {

    protected override string ResourceName => "WizBangs.xml";

    private static WizBangTemplateManager s_templateManager;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_templateManager)) {
            Logger.Error("Could not deserialize {0} as {1}", 
                Logger.Args(ResourceName, nameof(WizBangTemplateManager)));

            return;
        }

        Logger.Information("Loaded {0} sigils.", Logger.Args(s_templateManager.m_templates.Count));
    }

    /// <summary>
    /// Checks if a WizBang with the specified name exists.
    /// </summary>
    /// <param name="wizBangName">The name of the WizBang to check.</param>
    /// <returns><c>true</c> if a WizBang with the specified name exists; otherwise, <c>false</c>.</returns>
    internal static bool DoesWizBangExist(string wizBangName)
        => s_templateManager.m_templates.Any(x => x.m_name == wizBangName);

    public void DisposeStream() => base.Stream.Dispose();

}
