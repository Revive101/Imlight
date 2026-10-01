using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// The state of one zone instance: the tokens, counters and puzzle variables that zone triggers keep, the current
/// state of every named object (a lever's "On", a door's "Idle_Open"), and the dungeon quest progress. Trigger results write
/// them (ResZoneToken*, ResZoneCounter, ResEncounterSetVariable) and trigger requirements read them
/// (ReqZoneToken, ReqZoneTokenValue, ReqZoneCounter, ReqGetEncounterVariable). The state lives and dies with the
/// zone, so every instance keeps its own progress. Per-player tokens are keyed by the player's game object id,
/// which is the same after a relog.
/// </summary>
public sealed class ZoneScriptState {

    private sealed class Token {

        public bool Enabled;
        public int Value;

    }

    private readonly ConcurrentDictionary<string, Token> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _triggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _triggerFires = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _objectStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _changedObjects = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _questClaims = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completedQuests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _completedGoals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    private const string COUNTER_SET = "ZCA_Set";

    /// <summary>
    /// Applies a token, counter or puzzle-variable result for the player (their game object id, 0 for none) who triggered it.
    /// </summary>
    /// <returns>False when the result is not one this state keeps.</returns>
    public bool Apply(Result result, ulong player) {
        lock (_lock) {
            switch (result) {
                case ResZoneTokenEnable enable:
                    GetToken(enable.m_tokenName, enable.m_zoneWide, player).Enabled = true;
                    return true;
                case ResZoneTokenDisable disable:
                    GetToken(disable.m_tokenName, disable.m_zoneWide, player).Enabled = false;
                    return true;
                case ResZoneTokenModify modify:
                    GetToken(modify.m_tokenName, modify.m_zoneWide, player).Value += modify.m_delta;
                    return true;
                case ResZoneTokenReset reset: {
                    var token = GetToken(reset.m_tokenName, reset.m_zoneWide, player);
                    token.Enabled = false;
                    token.Value = 0;
                    return true;
                }
                case ResZoneCounter counter:
                    ApplyCounter(counter);
                    return true;
                case ResEncounterSetVariable variable:
                    _variables[VariableKey(variable.m_questName, variable.m_varName)] = variable.m_value;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// True when the token is enabled, for the whole zone or for this player.
    /// </summary>
    public bool IsTokenEnabled(string name, ulong player)
        => _tokens.TryGetValue(TokenKey(name, true, 0), out var zoneToken) && zoneToken.Enabled
        || player != 0 && _tokens.TryGetValue(TokenKey(name, false, player), out var playerToken) && playerToken.Enabled;

    /// <summary>
    /// The token's value: the zone-wide one when the token is zone-wide, else the player's.
    /// </summary>
    public int GetTokenValue(string name, ulong player) {
        if (_tokens.TryGetValue(TokenKey(name, true, 0), out var zoneToken)) {
            return zoneToken.Value;
        }

        return player != 0 && _tokens.TryGetValue(TokenKey(name, false, player), out var playerToken)
            ? playerToken.Value
            : 0;
    }

    /// <summary>
    /// Records whether a trigger is enabled (the zone's trigger supervisor keeps this current).
    /// </summary>
    public void SetTriggerEnabled(string triggerName, bool enabled) {
        if (!string.IsNullOrEmpty(triggerName)) {
            _triggers.AddOrUpdate(triggerName, enabled, (_, old) => enabled);
        }
    }

    /// <summary>
    /// True when the trigger is enabled; a trigger the zone does not have is not.
    /// </summary>
    public bool IsTriggerEnabled(string triggerName)
        => !string.IsNullOrEmpty(triggerName) && _triggers.TryGetValue(triggerName, out var enabled) && enabled;

    /// <summary>
    /// True when the trigger may still fire under its m_triggerMax: 0 and 0xFFFFFFFF (the client data's "no limit")
    /// are unlimited, any other value is the most fires the zone instance allows.
    /// </summary>
    public bool HasTriggerFiresLeft(string triggerName, uint triggerMax)
        => triggerMax == 0 || triggerMax == uint.MaxValue || GetTriggerFires(triggerName) < triggerMax;

    /// <summary>
    /// How many times the trigger has fired in this zone instance.
    /// </summary>
    public int GetTriggerFires(string triggerName)
        => !string.IsNullOrEmpty(triggerName) && _triggerFires.TryGetValue(triggerName, out var fires) ? fires : 0;

    /// <summary>
    /// Counts one fire of the trigger in this zone instance.
    /// </summary>
    public void RecordTriggerFire(string triggerName) {
        if (!string.IsNullOrEmpty(triggerName)) {
            _triggerFires.AddOrUpdate(triggerName, 1, (_, old) => old + 1);
        }
    }

    public int GetCounter(string name)
        => !string.IsNullOrEmpty(name) && _counters.TryGetValue(name, out var value) ? value : 0;

    public bool GetVariable(string questName, string varName)
        => _variables.TryGetValue(VariableKey(questName, varName), out var value) && value;

    private void ApplyCounter(ResZoneCounter counter) {
        var name = counter.m_counterName;
        if (string.IsNullOrEmpty(name)) {
            return;
        }

        var current = GetCounter(name);

        // The client data has two actions: ZCA_Set and ZCA_Add (the value may be negative).
        _counters[name] = string.Equals(counter.m_action, COUNTER_SET, StringComparison.OrdinalIgnoreCase)
            ? counter.m_value
            : current + counter.m_value;
    }

    private Token GetToken(string name, bool zoneWide, ulong player)
        => _tokens.GetOrAdd(TokenKey(name, zoneWide || player == 0, player), _ => new Token());

    private static string TokenKey(string name, bool zoneWide, ulong player)
        => zoneWide ? $"Z|{name}" : $"P|{player}|{name}";

    private static string VariableKey(string questName, string varName)
        => $"{questName}|{varName}";

    /// <summary>
    /// Records a dungeon quest step of this instance and returns whether it is the first time the instance saw it.
    /// </summary>
    public bool TryClaimQuestStep(InstanceQuestClaimKind kind, string questName, string goalName) {
        lock (_lock) {
            if (!_questClaims.Add($"{kind}|{questName}|{goalName}")) {
                return false;
            }

            switch (kind) {
                case InstanceQuestClaimKind.GoalComplete:
                    if (!_completedGoals.TryGetValue(questName, out var goals)) {
                        goals = [];
                        _completedGoals[questName] = goals;
                    }

                    goals.Add(goalName);
                    break;
                case InstanceQuestClaimKind.QuestComplete:
                    _completedQuests.Add(questName);
                    break;
            }

            return true;
        }
    }

    /// <summary>
    /// The dungeon quests this instance finished and, per quest, the goals it completed in order.
    /// </summary>
    public ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTSRSP SnapshotQuestProgress() {
        lock (_lock) {
            return new() {
                IsInstance = true,
                CompletedQuests = [.. _completedQuests],
                CompletedGoals = _completedGoals.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()),
            };
        }
    }

    /// <summary>
    /// Records the state an object enters. Returns false when it was already in that state.
    /// </summary>
    public bool SetObjectState(string objectName, string state) {
        if (string.IsNullOrEmpty(objectName)) {
            return false;
        }

        var changed = false;
        _changedObjects[objectName] = 0;
        _objectStates.AddOrUpdate(
            objectName,
            _ => { changed = true; return state ?? string.Empty; },
            (_, old) => { changed = !string.Equals(old, state, StringComparison.Ordinal); return state ?? string.Empty; });

        return changed;
    }

    /// <summary>
    /// Records the state an object starts in, unless it has already been changed.
    /// </summary>
    public void SeedObjectDefault(string objectName, string state) {
        if (!string.IsNullOrEmpty(objectName) && !string.IsNullOrEmpty(state)) {
            _objectStates.TryAdd(objectName, state);
        }
    }

    public string GetObjectState(string objectName)
        => !string.IsNullOrEmpty(objectName) && _objectStates.TryGetValue(objectName, out var state) ? state : null;

    public bool IsObjectIn(string objectName, string state)
        => string.Equals(GetObjectState(objectName), state, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The state of an object something changed since the zone loaded, else null.
    /// </summary>
    public string GetObjectStateIfChanged(string objectName)
        => !string.IsNullOrEmpty(objectName) && _changedObjects.ContainsKey(objectName) ? GetObjectState(objectName) : null;

    /// <summary>
    /// Every object something changed since the zone loaded, with its state.
    /// </summary>
    public KeyValuePair<string, string>[] SnapshotChangedObjects()
        => [.. _objectStates.Where(kv => _changedObjects.ContainsKey(kv.Key))];

}
