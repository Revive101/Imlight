/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.IO;
using Imlight.Common.Utilities;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

public enum MagicSchoolEnum {
    Fire = 2343174,
    Ice = 72777,
    Storm = 83375795,
    Life = 2330892,
    Myth = 2448141,
    Death = 78318724,
    Balance = 1027491821,
}

[Serializable]
public class Wizard : IDisposable {
    private const float OrientationCompressionFactor = CharacterHelper.OrientationCompressionFactor;

    public ulong AccountId { get; set; }               // <
    public ulong CharId { get; set; }                  //  | These values are never subject to change.
    public uint NameIndices { get; set; }              //  |
    public WideByteString NameOverride { get; set; }   // <
    public MagicSchoolEnum WizardSchool { get; set; }
    public byte Level { get; set; }
    public int TrainingPoints { get; set; }
    public int XpToNextLevel { get; set; }
    public string Zone { get; set; }
    public string ZoneDisplayName { get; set; }
    public byte World { get; set; }
    public Vector3 Location {
        get => this.GameObject?.m_location ?? _location;
        set {
            if (this.GameObject is not null) {
                this.GameObject.m_location = value;
            }
            else {
                _location = value;
            }
        }
    }
    public Vector3 Orientation {
        get => this.GameObject?.m_orientation ?? _orientation;
        set {
            if (this.GameObject is not null) {
                this.GameObject.m_orientation = value;
            }
            else {
                _orientation = value;
            }
        }
    }
    public WizardCharacterBehavior WizardAvatar { get; set; }
    public WizGameStats GameStats { get; set; }
    private readonly List<WizClientObjectItem> _objectItems;

    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;

    // Empty constructor for deserialization.
    [JsonConstructor] public Wizard() { }

    public Wizard(MagicSchoolEnum wizardSchoolType, WizardCharacterBehavior avatar, uint nameIndices, byte level = 1) {
        this.CharId = RandomGen.GenerateGUID();
        this.WizardSchool = wizardSchoolType;
        this.WizardAvatar = avatar;
        this.NameIndices = nameIndices;
        this.Level = level;
        this.Zone = ConfigurationManager.Settings.StartingZone;
        this.World = ConfigurationManager.Settings.StartingWorld;
        this.GameStats = new WizGameStats();

        this._objectItems = new List<WizClientObjectItem>();
    }

    public void SetLocation(Vector3 loc) {
        this.Location = loc;

        // Persistent save.
        CharacterCollection.UpdateCharacterLocation(this, loc, Orientation.Z);
    }

    public void SetOrientation(byte direction) {
        this.Orientation = new Vector3(0, 0, direction * OrientationCompressionFactor);

        // Persistent save.
        CharacterCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }

    public void SetZone(string zone, string zoneDisplayName) {
        this.Zone = zone;
        this.ZoneDisplayName = zoneDisplayName;

        // Persistent save.
        CharacterCollection.UpdateCharacterZone(this, zone, zoneDisplayName);
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

    public void Dispose() {
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        CharacterCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }
}
