/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class VendorComponent: BaseZoneComponent, IServiceComponent, IComponentFactory {

    public string ServiceName => "WizShoppingService";
    public string NpcIcon     => "Shopping";
    public string NpcNameKey  => "NPCFormats_Name";
    public string NpcTextKey  => "GUI_NPCInteractText";
    public string WizBang     => "Shopping";
    private static string DisplayKey => "GUI_ShopOptionEquipment";
    private readonly List<GID> _inventory;

    // ctor
    internal VendorComponent(ZoneEntity entity) : base(entity) {
        if (entity.Template is not GameObjectTemplate goTemplate) {
            throw new System.Exception("VendorComponent can only be attached to GameObjects");
        }

        if (!NpcInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out var inventory)) {
            throw new System.Exception("VendorComponent requires an NPC inventory");
        }

        _inventory = inventory.Inventory;
    }

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate goTemplate 
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate) 
        && NpcInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out _);

    public IEnumerable<ServiceOptionBase> GetServiceOptions() 
        => [
            new EquipmentShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

}