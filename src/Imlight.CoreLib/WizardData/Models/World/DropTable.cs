/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.World;

public class DropTable {

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double RollChance { get; set; } = 1.0;
    public int Weight { get; set; } = 100;
    public double NoneChance { get; set; } = 0.0;
    public double PityCounter { get; set; } = 0.0;
    public List<DropItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = Environment.UserName;
    public string ModifiedBy { get; set; } = Environment.UserName;

}

public class DropItem {

    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public RequirementList Requirements { get; set; } = null!;

}