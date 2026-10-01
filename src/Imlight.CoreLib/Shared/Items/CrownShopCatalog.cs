using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imcodec.IO;

using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Shared.Items;

/// <summary>A local catalog built from this client's item templates, with server-owned prices.</summary>
public sealed class CrownShopCatalog {
    // Stay below MessageEncoder's 0x777F large-packet threshold, including
    // the PCS response fields. Its large-packet framing is not client-compatible.
    public const int PageSize = 1000;
    public const int MaxPageBytes = 28_000;
    public sealed record Entry(uint Id, string Name, string DisplayName, int Gold, int Crowns, int CategoryId);
    public IReadOnlyList<Entry> Entries { get; }
    private readonly Dictionary<ulong, Entry> _byId;
    private static readonly Lazy<CrownShopCatalog> s_instance = new(() => {
        _ = CoreObjectFactory.Instance;
        var templates = CoreObjectFactory.TemplateManifest.m_serializedTemplates
            .Where(location => location.m_filename.ToString().StartsWith("ObjectData/", StringComparison.Ordinal))
            .Select(location => RootArchiveLoader.GetFile<CoreTemplate>(location.m_filename))
            .OfType<WizItemTemplate>();
        var catalog = new CrownShopCatalog(templates);
        Logger.Information("Loaded {0} local Crown Shop items; every entry has a gold price.", Logger.Args(catalog.Entries.Count));
        return catalog;
    });
    public static CrownShopCatalog Instance => s_instance.Value;

    public CrownShopCatalog(IEnumerable<WizItemTemplate> templates) {
        var supported = templates.Where(IsSupported).ToList();
        Entries = supported.Select(template => {
                var crowns = Price(template.m_creditsCost);
                return new Entry(template.m_templateID, template.m_objectName.ToString(),
                    Locale.GetEnglishName(template.m_displayName), checked(crowns * 10), crowns,
                    CrownShopSections.Classify(template));
            }).DistinctBy(entry => entry.Id)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            // Mix the sections so the first page contains mounts, pets, packs, gear, etc.
            // Pure alphabetical paging previously filled entire pages with one item type.
            .GroupBy(entry => entry.CategoryId)
            .SelectMany(group => group.Select((entry, index) => (entry, index)))
            .OrderBy(pair => pair.index).ThenBy(pair => pair.entry.CategoryId)
            .Select(pair => pair.entry).ToList();
        _byId = Entries.ToDictionary(entry => (ulong) entry.Id);
    }

    public static bool IsSupported(WizItemTemplate template) =>
        float.IsFinite(template.m_creditsCost) && (template.m_creditsCost > 0
        || template.m_adjectiveList.Any(tag => tag.ToString().Equals("FLAG_CrownsOnly", StringComparison.OrdinalIgnoreCase)));

    private static int Price(float value) => (int) Math.Clamp(Math.Ceiling(value), 1, int.MaxValue);
    public bool TryGet(ulong id, out Entry entry) => _byId.TryGetValue(id, out entry);

    public static ByteString SerializeSegmentation(SegmentationInputData data) {
        // PermanentShop::HandleSegData reads an ordinary object with mask 24.
        // Unlike the catalog, this packet has no flags prefix or compression.
        var serializer = new ObjectSerializer(false, SerializerFlags.None);
        if (!serializer.Serialize(data, 24, out var bytes))
            throw new InvalidOperationException("Could not serialize Crown Shop eligibility data.");
        return bytes;
    }

    public ByteString SerializePage(string search, int page, out int pageCount) {
        var entries = Entries.Where(entry => string.IsNullOrWhiteSpace(search)
            || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Id.ToString() == search).ToList();
        pageCount = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 1, pageCount);
        var pageEntries = entries.Skip((page - 1) * PageSize).Take(PageSize).ToList();
        var data = new CrownShopData {
            m_items = pageEntries.Select((entry, index) => new CrownShopItem {
                m_itemTemplateId = (GID) (ulong) entry.Id,
                m_goldCost = entry.Gold, m_crownsCost = entry.Crowns, m_ticketCost = -1,
                m_noGift = true,
                // TabDisplayPriorityList parses comma-separated category:priority pairs.
                // Without membership, the client decodes the item but never lists it.
                m_displayPriority = $"1:{index},{entry.CategoryId}:{index}",
            }).ToList(),
            m_crownShopLayout = CrownShopSections.CreateLayout(),
            m_recomendedItems = new LevelData {
                m_level = 1,
                m_categoryData = CrownShopSections.Sections.Select(section => new CategoryData {
                    m_id = section.Id,
                    m_items = pageEntries.Where(entry => section.Id == 1 || entry.CategoryId == section.Id)
                        .Select((entry, index) => new RecoItemData {
                            m_templateID = unchecked((int) entry.Id), m_rank = index, m_secondaryRank = index,
                        }).ToList(),
                }).ToList(),
            },
            m_crownShopSegReqsSummary = new CrownShopSegReqsSummary(),
        };
        // The client's PCS reader uses serializer flags 9 and property mask 5.
        var serializer = new ObjectSerializer(false, SerializerFlags.SerializeFlags | SerializerFlags.Compress);
        if (!serializer.Serialize(data, 5, out var bytes) || bytes.Length > MaxPageBytes)
            throw new InvalidOperationException("Crown Shop page exceeds the safe network packet size.");
        return bytes;
    }
}
