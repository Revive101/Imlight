/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.World;

internal class AuctionHouseModel {
    public List<AuctionHouseEntry> AuctionHouseEntries { get; set; } = new();
}
