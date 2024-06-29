/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

/*
Notes:

State Catorgories:

A "StateSet" has a number of categories, which have their own independent states. For example. NPCs have a state set "NPCMobileStates,"
which has categories "Fundamental," "Primary," "Locomotive," and "NPCInteracting." Each category has its own set of states.
An object may be in multiple states at once, but only one state per category.

An npc's "fundamental" state may be "Alive," but in the "Locomotive" category, it may be "walking." These states are independent of each other,
but a state change in one category may force a state change in another category. For example, if an NPC dies, it may be forced to stop walking.

States themselves:

A "state" is a specific phase that an object is in. There are states for dead/alive, walking/running/idle, and interacting with objects. Being in a state
may change the object's behavior, like blocking movement or disallowing certain actions. Every time a state changes, the server will
update the client with the new state, so long as the state is not private (_privateState).

There are also state transitions. Each state will declare which states it is capable of transforming into. Some state changes may have an intermediate
state known as a "transition state." Transition states are explicitly declared with the `m_autoTransition` field, and have a `m_transitionTime` field.
When a state is changed to a state that has a transition state, the object will first enter the transition state, then the final state.

Entering a state may also play an animation.

A state also has a number of blocked states that it cannot be in while that state is active. The blocked states extend beyond the category of the state.
*/

using System;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.Game.States;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

public sealed class ServerObjectStateBehavior : ServerBehaviorInstance {
    public override bool NoTransfer { get; set; } = true;

    private readonly string _stateSetName;
    private readonly ObjStateSet _stateSet;

    // ctor
    public ServerObjectStateBehavior(string stateSetName) {
        _stateSetName = stateSetName;
        _stateSet = StateFactory.GetStateSet(stateSetName)
            ?? throw new ArgumentException($"StateSet '{stateSetName}' not found."); ;

        SetDefaultStates();
    }

    // This is a no transfer behavior !!
    public override BehaviorInstance GetClientBehaviorInstance() => throw new NotImplementedException();

    public void SetState(string categoryName, string stateName) {
        // Determine if the category and state given exist
        var category = GetCategory(categoryName);
        if (category == null) {
            Logger.Error("Category {0} not found in {1} {2}", Logger.Args(categoryName, typeof(ObjStateSet), _stateSet));
            return;
        }

        var newState = GetState(categoryName, stateName);
        if (newState == null) {
            Logger.Error("State {0} not found in {1} {2}", Logger.Args(stateName, typeof(ObjStateCategory), category));
            return;
        }

        // Determine if this transition is possible given the current state.
        // We'll throw a warning if the state is not allowed to transition to the new state, but continue regardless
        // because we are the server and we can do whatever we want.
        var currentState = GetCategoryState(categoryName);
        var transition = GetTransitionFromCurrentState(categoryName, stateName);
        if (transition is null) {
            Logger.Warning("Transition from {0} to {1} not found in {2} {3}",
                Logger.Args(currentState, newState, typeof(ObjStateCategory), category));
        }

        // The new state may force changes in the other categories.
        foreach (var forceChange in newState.m_forcedStates) {
            SetState(forceChange.m_category, forceChange.m_forcedState);
        }

        category.m_baseState = newState.m_stateName;
    }

    public string this[string categoryName] {
        get {
            var category = GetCategory(categoryName);
            if (category == null) {
                Logger.Error("Category {0} not found in {1} {2}", Logger.Args(categoryName, typeof(ObjStateSet), _stateSet));
                return null;
            }

            return GetCategoryState(categoryName)?.m_stateName;
        }
        set => SetState(categoryName, value);
    }

    private void SetDefaultStates() {
        foreach (var category in _stateSet.m_categories) {
            category.m_baseState = category.m_startState;
        }
    }

    private ObjStateCategory GetCategory(string categoryName)
        => _stateSet.m_categories.FirstOrDefault(x => x.m_categoryName == categoryName);

    private ObjState GetState(string categoryName, string stateName)
        => GetCategory(categoryName)?.m_states.FirstOrDefault(x => x.m_stateName == stateName);

    private ObjState GetCategoryState(string categoryName)
        => GetCategory(categoryName)?.m_states.FirstOrDefault(x => x.m_stateName == GetCategory(categoryName)?.m_baseState);

    private ObjStateTransition GetTransitionFromCurrentState(string categoryName, string stateName)
        => GetCategoryState(categoryName)?.m_transitions.FirstOrDefault(x => x.m_targetState == stateName);
}
