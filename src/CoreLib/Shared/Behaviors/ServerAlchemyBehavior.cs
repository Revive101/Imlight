/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Imlight.Common;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerAlchemyBehavior : ServerBehaviorInstance {
    [JsonIgnore] public override bool NoTransfer { get; set; } = false;

    private static readonly int s_maxReagentsAllowed = 999;
    private static readonly int s_maxReagentStackAllowed = 99;
    private static readonly int s_maxCraftingSlots = 10;
    private static readonly int s_maxRecipes = 999;

    public List<ulong> ReagentItemIds { get; set; }

    [JsonIgnore] public List<ClientReagentItem> Reagents { get; set; }
    [JsonIgnore] public List<CraftingSlot> CraftingSlots { get; set; }
    [JsonIgnore] public List<Recipe> Recipes { get; set; }

    public override ClientAlchemyBehavior GetClientBehaviorInstance() => new() {
        m_reagentBag = new ObjectBag() {
            m_maxItemStack = s_maxReagentsAllowed,
            m_itemList = Reagents?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_craftingSlotsBag = new ObjectBag() {
            m_maxItemStack = s_maxCraftingSlots,
            m_itemList = CraftingSlots?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_recipeBag = new RecipeBag() {
            m_maxItemStack = s_maxRecipes,
            m_itemList = Recipes?.ConvertAll(item => (CoreObject) item) ?? []
        },
        m_maxReagentStack = s_maxReagentStackAllowed
    };
}
