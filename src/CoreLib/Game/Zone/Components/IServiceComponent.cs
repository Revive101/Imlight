/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Interface for service components that can be attached to a zone entity.
/// </summary>
public interface IServiceComponent {

    IEnumerable<ServiceOptionBase> GetServiceOptions();
    
    string ServiceName { get; }
    string NpcIcon { get; }
    string NpcNameKey { get; }
    string NpcTextKey { get; }
    string WizBang { get; } 

}