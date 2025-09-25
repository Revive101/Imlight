/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Results;

/// <summary>
/// Interface that provides context for result execution
/// </summary>
public interface IResultContext {

    /// <summary>
    /// Gets the results that should be executed
    /// </summary>
    /// <returns>Collection of results to execute</returns>
    IEnumerable<Result> GetResults();

    /// <summary>
    /// Gets the zone actor reference if applicable
    /// </summary>
    /// <returns>Zone actor reference or null if not in zone context</returns>
    IActorRef GetZoneActor();

    /// <summary>
    /// Gets the player actor reference
    /// </summary>
    /// <returns>Player actor reference</returns>
    IActorRef GetPlayerRef();

    /// <summary>
    /// Gets the player object
    /// </summary>
    /// <returns>Player object</returns>
    CoreObject GetPlayerObj();

    /// <summary>
    /// Gets the reply-to actor reference
    /// </summary>
    /// <returns>Reply-to actor reference or null if no reply needed</returns>
    IActorRef GetReplyTo();

}