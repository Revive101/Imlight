/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Threading.Tasks;

namespace Imlight.Server.WizardData;

public interface IChangeCache
{
    void EnqueueChange(object change);
    Task FlushChangesAsync();
}