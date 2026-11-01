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
 * BADGE CATALOG
 * ========================================================================
 *
 * PURPOSE:
 * Validated, indexed view of the SpiralDB badge definitions: lookups by id and name, per-filter order and
 * totals, counter families and the auto-add records.
 *
 * USAGE EXAMPLE:
 * if (BadgeCatalog.Current.TryGet(badgeId, out var definition)) { ... }
 *
 * NOTE:
 * Built once on first use, after SpiralDB.Load() ran at boot (it loads the badge definitions and the quest templates the earnable set reads). The instance never changes, so any actor may read it.
 * A record with a bad name hash, a repeated Index, no filter or an unknown required badge is skipped with a warning.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Badges;

internal sealed class BadgeCatalog {

    internal const string NO_TITLE_BADGE_NAME = "NoTitle";
    internal static readonly uint s_noTitleId = StringHash.Compute(NO_TITLE_BADGE_NAME);

    private static readonly Lock s_lock = new();

    private readonly Dictionary<uint, BadgeDefinition> _byId = [];
    private readonly Dictionary<uint, uint> _filterIndexById = [];
    private readonly Dictionary<uint, BadgeFilter> _filtersById = [];
    private readonly Dictionary<string, List<BadgeDefinition>> _familiesByCounterKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _counterKeysByAdjective = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<uint> _earnable = [];

    internal bool IsReady { get; private set; }
    internal int Count => Records.Count;
    internal IReadOnlyList<BadgeDefinition> Records { get; private set; } = [];
    internal IReadOnlyList<BadgeFilter> Filters { get; private set; } = [];
    internal IReadOnlyList<BadgeDefinition> AutoAddRecords { get; private set; } = [];

    internal sealed record BadgeFilter(uint Id, string Name, int Total, int ExtraCredit);

    internal static BadgeCatalog Current {
        get {
            lock (s_lock) {
                return field ??= Create(BadgeDefinitionCollection.GetAll(), QuestTemplateCollection.GetAllQuests());
            }
        }
    }

    internal static BadgeCatalog Create(IEnumerable<BadgeDefinition> definitions) 
        => Create(definitions, []);

    internal static BadgeCatalog Create(IEnumerable<BadgeDefinition> definitions, IEnumerable<QuestTemplate> quests) {
        var catalog = new BadgeCatalog();
        catalog.Build(definitions, quests);

        return catalog;
    }

    internal static string GetCounterKey(BadgeDefinition definition)
        => !string.IsNullOrEmpty(definition.RegistryName) ? definition.RegistryName
            : !string.IsNullOrEmpty(definition.ProgressInfo) ? definition.ProgressInfo
            : definition.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static uint GetFilterId(BadgeDefinition definition)
        => StringHash.Compute(definition.Filter);

    internal bool TryGet(uint id, out BadgeDefinition definition)
        => _byId.TryGetValue(id, out definition);

    internal bool TryGetByName(string name, out BadgeDefinition definition) {
        definition = null;

        return !string.IsNullOrEmpty(name) && _byId.TryGetValue(StringHash.Compute(name), out definition);
    }

    internal bool IsEarnable(uint id)
        => _earnable.Contains(id);

    internal bool TryGetFilter(uint filterId, out BadgeFilter filter)
        => _filtersById.TryGetValue(filterId, out filter);

    internal uint GetFilterIndex(uint id)
        => _filterIndexById.GetValueOrDefault(id);

    internal IReadOnlyList<BadgeDefinition> GetFamily(string counterKey)
        => _familiesByCounterKey.TryGetValue(counterKey ?? string.Empty, out var family) ? family : [];

    internal IReadOnlyList<string> GetCounterKeysForAdjective(string adjective)
        => _counterKeysByAdjective.TryGetValue(adjective ?? string.Empty, out var keys) ? keys : [];

    internal IReadOnlyList<string> GetCounterKeysForAdjectives(IEnumerable<string> adjectives)
        => [.. (adjectives ?? []).SelectMany(GetCounterKeysForAdjective).Distinct(StringComparer.Ordinal)];

    internal IEnumerable<string> CounterKeys => _familiesByCounterKey.Keys;

    internal int EarnableCount => _earnable.Count;

    private void Build(IEnumerable<BadgeDefinition> definitions, IEnumerable<QuestTemplate> quests) {
        var accepted = Validate(definitions);
        RemoveUnknownRequirements(accepted);

        Records = [.. accepted.OrderBy(d => d.Index).ThenBy(d => d.Id)];
        foreach (var definition in Records) {
            _byId[definition.Id] = definition;
        }

        BuildFilters();
        BuildCounters();
        AutoAddRecords = [.. Records.Where(d => d.AutoAdd && d.Requirements is not null)];
        BuildEarnable(quests);

        IsReady = Records.Count > 0 && _byId.ContainsKey(s_noTitleId);
        if (IsReady) {
            Logger.Information("Badge catalog: {0} of {1} badges can be earned.", Logger.Args(EarnableCount, Records.Count));

            return;
        }

        var noTitleState = _byId.ContainsKey(s_noTitleId) ? "present" : "missing";
        Logger.Error("Badge catalog is not ready: {0} badge definitions and the {1} record is {2}. Badges are not sent.",
            Logger.Args(Records.Count, NO_TITLE_BADGE_NAME, noTitleState));
    }

    private static List<BadgeDefinition> Validate(IEnumerable<BadgeDefinition> definitions) {
        var accepted = new List<BadgeDefinition>();
        var seenIds = new HashSet<uint>();
        var idByIndex = new Dictionary<uint, uint>();

        foreach (var definition in definitions.Where(d => d is not null).OrderBy(d => d.Id)) {
            if (IsValid(definition, seenIds, idByIndex)) {
                accepted.Add(definition);
            }
        }

        return accepted;
    }

    private static bool IsValid(BadgeDefinition definition, HashSet<uint> seenIds, Dictionary<uint, uint> idByIndex) {
        if (definition.Id == 0) {
            Logger.Warning("Badge definition without an id skipped (name {0}).",
                Logger.Args(definition.Name ?? definition.DisplayName));

            return false;
        }

        if (!seenIds.Add(definition.Id)) {
            Logger.Warning("Duplicate badge definition id {0} skipped.",
                Logger.Args(definition.Id));

            return false;
        }

        if (!string.IsNullOrEmpty(definition.Name) && StringHash.Compute(definition.Name) != definition.Id) {
            Logger.Warning("Badge definition {0} skipped: name {1} hashes to {2}.",
                Logger.Args(definition.Id, definition.Name, StringHash.Compute(definition.Name)));

            return false;
        }

        if (string.IsNullOrEmpty(definition.Filter)) {
            Logger.Warning("Badge definition {0} skipped: no filter.",
                Logger.Args(definition.Id));

            return false;
        }

        if (!definition.Dynamic && !idByIndex.TryAdd(definition.Index, definition.Id)) {
            Logger.Warning("Badge definition {0} skipped: index {1} is already used by {2}.",
                Logger.Args(definition.Id, definition.Index, idByIndex[definition.Index]));

            return false;
        }

        return true;
    }

    private static void RemoveUnknownRequirements(List<BadgeDefinition> accepted) {
        // Dropping one record can leave another one requiring it, so repeat until nothing changes.
        bool removed;
        do {
            removed = false;
            var ids = accepted.Select(d => d.Id).ToHashSet();
            foreach (var definition in accepted.ToList()) {
                var unknown = definition.RequiredAutoAddBadges.Where(id => !ids.Contains(id)).ToList();
                if (unknown.Count == 0) {
                    continue;
                }

                Logger.Warning("Badge definition {0} skipped: required auto-add badge {1} is unknown.",
                    Logger.Args(definition.Id, unknown[0]));
                accepted.Remove(definition);
                removed = true;
            }
        } while (removed);
    }

    private void BuildFilters() {
        var filters = new List<BadgeFilter>();
        foreach (var group in Records.GroupBy(GetFilterId)) {
            var inOrder = group.OrderBy(d => d.Index).ThenBy(d => d.Id).ToList();
            for (var rank = 0; rank < inOrder.Count; rank++) {
                _filterIndexById[inOrder[rank].Id] = (uint) rank;
            }

            var filter = new BadgeFilter(group.Key, inOrder[0].Filter, inOrder.Count, inOrder.Count(d => d.ExtraCredit));
            _filtersById[filter.Id] = filter;
            filters.Add(filter);
        }

        Filters = [.. filters.OrderBy(f => f.Id)];
    }

    private void BuildEarnable(IEnumerable<QuestTemplate> quests) {
        // A badge is earnable when Imlight has a way to award it: a kill counter,
        // an auto-add rule, a quest result or another earnable badge's result.
        var killCounterKeys = _counterKeysByAdjective.Values.SelectMany(keys => keys).ToHashSet(StringComparer.Ordinal);
        var questAwarded = QuestBadgeAwards.Find(quests).Select(StringHash.Compute).ToHashSet();
        var autoAdded = AutoAddRecords.Select(d => d.Id).ToHashSet();

        foreach (var definition in Records.Where(d => !d.Retired)) {
            var hasKillCounter = definition.Max > 0 && killCounterKeys.Contains(GetCounterKey(definition));
            if (hasKillCounter || autoAdded.Contains(definition.Id) || questAwarded.Contains(definition.Id)) {
                _earnable.Add(definition.Id);
            }
        }

        while (AddAwardedByEarnable()) { }
    }

    private bool AddAwardedByEarnable() {
        var added = false;
        foreach (var definition in Records.Where(d => d.CompleteResults is not null && _earnable.Contains(d.Id))) {
            foreach (var name in QuestBadgeAwards.Find(definition.CompleteResults)) {
                added |= TryAddEarnable(name);
            }
        }

        return added;
    }

    private bool TryAddEarnable(string badgeName) {
        if (!_byId.TryGetValue(StringHash.Compute(badgeName), out var awarded)) {
            return false;
        }

        return !awarded.Retired && _earnable.Add(awarded.Id);
    }

    private void BuildCounters() {
        foreach (var definition in Records.Where(d => d.Max > 0)) {
            var key = GetCounterKey(definition);
            GetOrAdd(_familiesByCounterKey, key).Add(definition);

            var adjectives = definition.Adjectives
                                       .Where(a => !string.IsNullOrEmpty(a))
                                       .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var adjective in adjectives) {
                var keys = GetOrAdd(_counterKeysByAdjective, adjective);
                if (!keys.Contains(key)) {
                    keys.Add(key);
                }
            }
        }

        foreach (var family in _familiesByCounterKey.Values) {
            family.Sort((a, b) => a.Max != b.Max ? a.Max.CompareTo(b.Max) : a.Id.CompareTo(b.Id));
        }
    }

    private static List<T> GetOrAdd<T>(Dictionary<string, List<T>> lists, string key) {
        if (!lists.TryGetValue(key, out var list)) {
            list = [];
            lists[key] = list;
        }

        return list;
    }

}
