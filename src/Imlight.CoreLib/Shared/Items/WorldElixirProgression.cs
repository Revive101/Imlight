using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Shared.Items;

/// <summary>Builds cumulative world skips from the client's quest lists and prerequisite chain.</summary>
public sealed class WorldElixirProgression {
    public sealed record Stage(string CompletionKey, IReadOnlyList<string> Quests,
        IReadOnlyList<string> SpellNames, IReadOnlyList<LevelUpElixirPropertyRegistryEntry> Registry);
    public sealed record Plan(uint TemplateId, string World, string CompletionKey, string Destination,
        int EndLevel, int MaxPotions, IReadOnlyList<Stage> Stages);
    private readonly Dictionary<uint, WizItemTemplate> _worlds;
    private readonly Dictionary<string, WizItemTemplate> _byCompletion;
    private readonly LevelUpElixirBehaviorTemplate _foundation;
    public IReadOnlyCollection<uint> TemplateIds => _worlds.Keys;
    private static readonly Lazy<WorldElixirProgression> s_instance = new(() => {
        _ = CoreObjectFactory.Instance;
        return new(CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .Where(location => location.m_filename.ToString().StartsWith("ObjectData/Elixirs/Elixir-World-", StringComparison.Ordinal)
                || location.m_filename.ToString() == "ObjectData/Elixirs/Elixir-Level50.xml")
            .Select(location => RootArchiveLoader.GetFile<WizItemTemplate>(location.m_filename)));
    });
    public static WorldElixirProgression Instance => s_instance.Value;

    public WorldElixirProgression(IEnumerable<WizItemTemplate> templates) {
        var all = templates.Where(template => template != null).ToList();
        _foundation = all.Single(template => template.m_objectName == "Elixir-Level50")
            .m_behaviors.OfType<LevelUpElixirBehaviorTemplate>().Single();
        _worlds = all.Where(template => template.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Any())
            .DistinctBy(template => template.m_templateID).ToDictionary(template => template.m_templateID);
        _byCompletion = _worlds.Values.ToDictionary(template => CompletionRequirement(template, true));
    }

    private static string CompletionRequirement(WizItemTemplate template, bool completed) =>
        template.m_purchaseRequirements.m_requirements.OfType<ReqHasEntry>()
            .Single(requirement => requirement.m_applyNOT == completed && requirement.m_entryName.EndsWith("_Complete", StringComparison.Ordinal))
            .m_entryName;

    public Plan CreatePlan(uint templateId, MagicSchool school) {
        if (!_worlds.TryGetValue(templateId, out var target))
            throw new InvalidOperationException("This world elixir has no supported progression data; no currency was charged.");
        var chain = new List<WizItemTemplate>();
        var visited = new HashSet<uint>();
        var next = target;
        while (next != null) {
            if (!visited.Add(next.m_templateID)) throw new InvalidOperationException("Cyclic world-elixir progression data.");
            chain.Add(next);
            var prerequisite = CompletionRequirement(next, false);
            if (_byCompletion.TryGetValue(prerequisite, out next)) continue;
            if (prerequisite != "DS-ACAD-C01-005_Complete")
                throw new InvalidOperationException("Missing earlier-world completion data; no currency was charged.");
            break;
        }
        chain.Reverse();
        var stages = new List<Stage>();
        // Reuse only first-arc progression/spells, not the Level 50 elixir's gear,
        // gold or optional Grizzleheim/Wintertusk quest grants.
        var foundation = MakeStage(_foundation, "DS-ACAD-C01-005_Complete", school);
        var prefixes = new[] { "WC-", "KT-", "MB-", "MS-", "DS-" };
        stages.Add(foundation with {
            Quests = foundation.Quests.Where(quest => prefixes.Any(prefix => quest.StartsWith(prefix, StringComparison.Ordinal))).ToArray(),
            Registry = foundation.Registry.Where(entry => !entry.m_registryEntryName.Contains("GH-", StringComparison.Ordinal)).ToArray(),
        });
        foreach (var template in chain)
            stages.Add(MakeStage(template.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Single(), CompletionRequirement(template, true), school));
        var behavior = target.m_behaviors.OfType<WorldElixirBehaviorTemplate>().Single();
        var destination = DataForSchool(behavior, school).Select(data => data.m_teleportToZoneOnComplete).LastOrDefault(zone => !string.IsNullOrWhiteSpace(zone));
        if (string.IsNullOrWhiteSpace(destination)) throw new InvalidOperationException("World elixir has no destination; no currency was charged.");
        return new(templateId, target.m_objectName.Replace("Elixir-World-", ""), CompletionRequirement(target, true), destination,
            50 + chain.Count * 10, behavior.m_maxPotions, stages);
    }

    private static IEnumerable<LevelUpElixirSchoolSpecificData> DataForSchool(LevelUpElixirBehaviorTemplate behavior, MagicSchool school) {
        if (behavior.m_allSchoolData != null) yield return behavior.m_allSchoolData;
        foreach (var data in behavior.m_schoolSpecificData.Where(data => data.m_schoolName.Equals(school.ToString(), StringComparison.OrdinalIgnoreCase)))
            yield return data;
    }

    private static Stage MakeStage(LevelUpElixirBehaviorTemplate behavior, string completion, MagicSchool school) {
        var data = DataForSchool(behavior, school).ToList();
        var quests = data.SelectMany(value => value.m_questsToComplete).Distinct(StringComparer.Ordinal).ToArray();
        if (!quests.Contains(completion[..^"_Complete".Length]))
            throw new InvalidOperationException("World elixir lacks its final quest; no currency was charged.");
        // Fail closed if future world templates add effects this implementation cannot deliver.
        if (behavior is WorldElixirBehaviorTemplate && (data.Any(value => value.m_questsToAdd.Count > 0
            || value.m_itemsToPlaceInInventory.Count > 0 || value.m_gearToEquip.Count > 0 || value.m_badgesToComplete.Count > 0
            || value.m_addTrainingPointIfQuestNotComplete.Count > 0) || behavior.m_gold != 0 || behavior.m_setCharacterToLevel > 0))
            throw new InvalidOperationException("This world elixir contains additional unsupported rewards; no currency was charged.");
        return new(completion, quests, data.SelectMany(value => value.m_spellsToGive).Distinct().ToArray(),
            data.SelectMany(value => value.m_propertyRegistryEntires).ToArray());
    }
}
