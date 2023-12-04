using Imlight.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

public class WizardInventory {
    private readonly List<WizClientObjectItem> _objectItems;

    public WizardInventory() {
        _objectItems = new List<WizClientObjectItem>();
    }

    public bool AddItem(WizClientObjectItem item) {
        if (item is null) {
            return false;
        }
        if (_objectItems.Any(i => i.m_globalID == item.m_globalID)) {
            Logger.Debug("Item with same global id {0} already exists in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        _objectItems.Add(item);
        return true;
    }

    public bool RemoveItem(WizClientObjectItem item) {
        if (item is null) {
            return false;
        }
        if (!_objectItems.Remove(item)) {
            Logger.Debug("Tried to remove item with global id {0} that does not exist in player inventory.", Logger.Args(item.m_globalID));
            return false;
        }

        return true;
    }

    public bool RemoveItem(ulong itemId) {
        var item = _objectItems.Find(i => i.m_globalID == itemId);
        if (item is not null) {
            _objectItems.Remove(item);
            return true;
        }

        return false;
    }

    public bool HasItem(ulong itemId) {
        return _objectItems.Any(i => i.m_globalID == itemId);
    }

    public WizClientObjectItem GetItem(ulong itemId) {
        return _objectItems.Find(i => i.m_globalID == itemId);
    }
}
