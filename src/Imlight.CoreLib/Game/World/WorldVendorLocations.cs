/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.World;

public class WorldVendorLocations : RootSingleResourceSingleton<WorldVendorLocations>, IMemoryStreamDisposable {
    protected override string ResourceName => "VendorLocationData.xml";

    private static ObjectLocationList s_vendorLocationMap;

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        var propClass = serializer.OpenClass<ObjectLocationList>(Stream);

        s_vendorLocationMap = propClass;

        Logger.Information("Loaded {0} vendor locations", Logger.Args(s_vendorLocationMap.m_objectList.Count));
    }

    internal static bool IsVendor(uint templateId) => s_vendorLocationMap.m_objectList.Any(x => x.m_templateID == templateId);

    public void DisposeStream() => Stream.Dispose();
}
