/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class VendorComponent(ZoneEntity entity) : BaseZoneComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "WizShoppingService";
    public string NpcIcon     => null;
    public string NpcNameKey  => null;
    public string NpcTextKey  => null;
    public string WizBang     => "Shopping";
    private static string DisplayKey => "GUI_ShopOptionEquipment";
    private readonly List<GID> _inventory;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        // Attach if the template is an NPC and has an inventory in Dragon database,
        // or if the template is a vendor as per game client data.
        => template is GameObjectTemplate goTemplate 
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate) 
        && (NpcInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out _)
        || WorldVendorLocations.IsVendor(goTemplate.m_templateID));

    public IEnumerable<ServiceOptionBase> GetServiceOptions() 
        => [
            new EquipmentShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

}