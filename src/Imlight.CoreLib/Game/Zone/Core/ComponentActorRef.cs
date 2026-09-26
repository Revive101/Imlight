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
 *
 * ========================================================================
 * COMPONENT FRAMEWORK
 * ========================================================================
 * 
 * PURPOSE:
 * The actor reference of a component hosted inside its entity's actor. Anything told to it
 * reaches that one component, in order, on the entity's thread.
 * 
 * USAGE EXAMPLE:
 * duelComponent.ActorRef.Tell(action, Self);
 * 
 * NOTE:
 * Ask, Forward and PipeTo work as with any actor reference. The path is the entity's path plus the
 * component type name.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using Akka.Actor;

namespace Imlight.CoreLib.Game.Zone.Core;

internal sealed record ComponentMessage(ZoneEntityComponent Component, object Message);

internal sealed class ComponentActorRef(IInternalActorRef entityRef, ZoneEntityComponent component) : MinimalActorRef {

    public override ActorPath Path { get; } = entityRef.Path / component.GetType().Name;

    public override IActorRefProvider Provider => entityRef.Provider;

    public override IInternalActorRef Parent => entityRef;

    protected override void TellInternal(object message, IActorRef sender)
        => entityRef.Tell(new ComponentMessage(component, message), sender);

}
