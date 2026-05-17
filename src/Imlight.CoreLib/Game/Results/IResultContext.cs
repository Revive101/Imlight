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