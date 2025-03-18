/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.World;

public class WorldVendorLocations : RootSingleResourceSingleton<WorldVendorLocations>, IMemoryStreamDisposable {

    protected override string ResourceName => "VendorLocationData.xml";

    private static ObjectLocationList s_vendorLocationMap;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_vendorLocationMap)) {
            Logger.Error("Failed to deserialize vendor locations");

            return;
        }

        Logger.Information("Loaded {0} vendor locations", Logger.Args(s_vendorLocationMap.m_objectList.Count));
    }

    internal static bool IsVendor(uint templateId) => s_vendorLocationMap.m_objectList.Any(x => x.m_templateID == templateId);

    public void DisposeStream() => Stream.Dispose();

}
