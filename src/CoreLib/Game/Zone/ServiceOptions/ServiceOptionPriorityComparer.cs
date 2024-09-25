/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionPriorityComparer : IComparer<ServiceOption> {
    private readonly List<string> _priorities;

    public ServiceOptionPriorityComparer(List<string> priorities)
        => _priorities = priorities;

    public int Compare(ServiceOption x, ServiceOption y) {
        int indexX = _priorities.IndexOf(x.WizBang);
        int indexY = _priorities.IndexOf(y.WizBang);

        if (indexX == -1 || indexY == -1) {
            throw new ArgumentException("ServiceOption not found in priority list");
        }

        return indexX.CompareTo(indexY);
    }
}
