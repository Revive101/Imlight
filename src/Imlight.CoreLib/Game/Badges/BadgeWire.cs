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
 * BADGE WIRE
 * ========================================================================
 *
 * PURPOSE:
 * Builds the MSG_BADGES (and MSG_BADGESEGMENT) messages that carry a character's badges to the client.
 *
 * USAGE EXAMPLE:
 * foreach (var message in BadgeWire.Build(wizard, BadgeCatalog.Current, entries, full: true)) { SendToSocket(message); }
 *
 * NOTE:
 * BadgeInfo and BadgeFilterInfo are zlib-compressed serialized lists with the uncompressed length in front. 
 * A list too large for one string is cut into MSG_BADGESEGMENT chunks of one compressed stream; the last chunk travels in MSG_BADGES. 
 * Every message carries the complete filter list of the held badges.
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
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Badges;

internal static class BadgeWire {

    internal const int MaxChunkBytes = 30000;
    internal const int MaxChunks = 255;
    private const uint SerializationFlags = 4;

    internal static List<IMessage> Build(Wizard wizard, BadgeCatalog catalog, IReadOnlyCollection<BadgeDefinition> entries, bool full) {
        var state = wizard.BadgeBehavior;
        var ordered = full ? entries.OrderBy(d => d.Id) : entries.OrderBy(d => d.Index).ThenBy(d => d.Id);
        var badgeList = new BadgeInfoList { m_badges = [.. ordered.Select(d => ToBadgeInfo(state, catalog, d))] };
        var filterList = new BadgeFilterInfoList { m_badgeFilterInfoList = [.. GetHeldFilters(state, catalog)] };

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(badgeList, SerializationFlags, out var serializedBadges)
            || !serializer.Serialize(filterList, SerializationFlags, out var serializedFilters)) {
            Logger.Error("Failed to serialize the badge list of wizard {0}.", Logger.Args(wizard.CharId));

            return [];
        }

        byte[] packedBadges = Compression.CompressWithLength(serializedBadges);
        byte[] packedFilters = Compression.CompressWithLength(serializedFilters);

        var chunkCount = Math.Max(1, (packedBadges.Length + MaxChunkBytes - 1) / MaxChunkBytes);
        if (chunkCount > MaxChunks) {
            Logger.Error("The badge list of wizard {0} needs {1} segments and cannot be sent.", Logger.Args(wizard.CharId, chunkCount));

            return [];
        }

        var messages = new List<IMessage>();
        for (var segment = 0; segment < chunkCount - 1; segment++) {
            messages.Add(new GAME2_55_PROTOCOL.MSG_BADGESEGMENT {
                Data = packedBadges[(segment * MaxChunkBytes)..((segment + 1) * MaxChunkBytes)],
                Segment = (byte) segment
            });
        }

        var lastChunk = packedBadges[((chunkCount - 1) * MaxChunkBytes)..];
        messages.Add(new GAME_5_PROTOCOL.MSG_BADGES {
            CurrentBadge = full ? GetCurrentBadge(state, catalog) : 0,
            UpdateAll = (sbyte) (full ? 1 : 0),
            TotalBadges = full ? (uint) catalog.Count : 0,
            Add = 1,
            Remove = 0,
            BadgeName = string.Empty,
            BadgeInfo = lastChunk,
            BadgeNameID = 0,
            BadgeFilterInfo = packedFilters,
            Display = 1,
            LastSegment = (byte) (chunkCount > 1 ? 1 : 0)
        });

        return messages;
    }

    private static uint GetCurrentBadge(ServerBadgeBehavior state, BadgeCatalog catalog)
        => state.SelectedBadgeId != 0 && catalog.TryGet(state.SelectedBadgeId, out _) ? state.SelectedBadgeId : BadgeCatalog.s_noTitleId;

    private static IEnumerable<BadgeFilterInfo> GetHeldFilters(ServerBadgeBehavior state, BadgeCatalog catalog)
        => BadgeView.GetHeld(state, catalog)
            .Select(BadgeCatalog.GetFilterId)
            .Distinct()
            .Order()
            .Select(id => catalog.TryGetFilter(id, out var filter) ? filter : null)
            .Where(filter => filter is not null)
            .Select(filter => new BadgeFilterInfo {
                m_badgeFilterNameID = filter.Id,
                m_numberOfBadges = filter.Total,
                m_numberOfExtraCreditBadges = filter.ExtraCredit
            });

    private static BadgeInfo ToBadgeInfo(ServerBadgeBehavior state, BadgeCatalog catalog, BadgeDefinition definition) {
        var progress = BadgeView.GetProgress(state, definition);

        return new BadgeInfo {
            m_badgeNameID = definition.Id,
            m_badgeTitle = definition.Title,
            m_badgeInfo = definition.Info,
            m_badgeProgressInfo = definition.ProgressInfo,
            m_badgeProgress = progress,
            m_badgeMax = definition.Max,
            m_badgeComplete = BadgeView.IsComplete(state, definition),
            m_index = definition.Index,
            m_badgeOutline = definition.Outline,
            m_badgeImage = definition.Image,
            m_dynamicBadge = definition.Dynamic,
            m_badgeFilterID = BadgeCatalog.GetFilterId(definition),
            m_filterIndex = catalog.GetFilterIndex(definition.Id),
            m_overcount = false,
            m_hasLoot = false,
            m_badgeLoot = string.Empty,
            m_extraCredit = definition.ExtraCredit,
            m_overcountInterval = 0,
            m_recurringRewardsDescription = string.Empty,
            m_overcountLoot = string.Empty,
            m_originalProgress = progress,
            m_rewardOverrideDescription = string.Empty
        };
    }

}
