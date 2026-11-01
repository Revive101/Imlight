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
 * BADGE PURCHASE GATE
 * ========================================================================
 *
 * PURPOSE:
 * Tells shops and trainers whether a purchase requirement list denies the sale because of a badge Imlight can award.
 *
 * USAGE EXAMPLE:
 * if (BadgeGate.Blocks(template.m_purchaseRequirements, templateId, SessionActor.ActorRef, GetActiveGameObject(), wizard, SessionActor.GetZoneActor())) { ... }
 *
 * NOTE:
 * Only lists that name a badge are enforced, and only for badges the catalog calls earnable: 
 * a requirement on a badge no rule or quest awards counts as met, so such an item is not locked forever. 
 * The other requirement kinds in purchase lists are not all answered by the server yet, 
 * and an unanswered kind evaluates to false, which would deny items that sell today.
 *
 * TODO:
 * Enforce the other purchase requirement kinds once their handlers exist?
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Badges;

internal static class BadgeGate {

    private static readonly ConcurrentDictionary<ulong, byte> s_reportedTemplates = new();

    internal sealed class GateContext(RequirementList requirements, IActorRef playerRef, CoreObject playerObj, Wizard wizard, IActorRef zoneRef)
        : GenericRequirementContext(requirements, playerRef, playerObj, wizard, zoneRef) { }

    internal static bool Blocks(RequirementList requirements, ulong templateId, IActorRef playerRef, CoreObject playerObj, Wizard wizard, IActorRef zoneRef) {
        var badgeNames = GetBadgeNames(requirements);
        if (badgeNames.Count == 0) {
            return false;
        }

        var catalog = BadgeCatalog.Current;
        var unearnableNames = badgeNames.Where(name => !catalog.IsEarnable(StringHash.Compute(name))).ToList();
        ReportUnearnable(templateId, unearnableNames);

        var hasEarnableBadge = unearnableNames.Count < badgeNames.Count;
        if (!hasEarnableBadge) {
            return false;
        }

        var context = new GateContext(requirements, playerRef, playerObj, wizard, zoneRef);

        return !RequirementDispatcher.EvaluateRequirements(requirements, context);
    }

    private static List<string> GetBadgeNames(RequirementList requirements) {
        var names = new List<string>();
        CollectBadgeNames(requirements, names);

        return [.. names.Distinct(StringComparer.Ordinal)];
    }

    private static void ReportUnearnable(ulong templateId, List<string> unearnableNames) {
        if (unearnableNames.Count == 0 || !s_reportedTemplates.TryAdd(templateId, 0)) {
            return;
        }

        Logger.Debug("Template {0} requires {1}, which Imlight cannot award, so that part of its purchase requirement is not enforced.",
            Logger.Args(templateId, string.Join(", ", unearnableNames)));
    }

    private static void CollectBadgeNames(RequirementList requirements, List<string> names) {
        foreach (var requirement in requirements?.m_requirements ?? []) {
            switch (requirement) {
                case ReqHasBadge has:
                    names.Add(has.m_badgeName);
                    break;
                case ReqBaseHasBadge baseHas:
                    names.Add(baseHas.m_badgeName);
                    break;
                case RequirementList nested:
                    CollectBadgeNames(nested, names);
                    break;
            }
        }
    }

}
