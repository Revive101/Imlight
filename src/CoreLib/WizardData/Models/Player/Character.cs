/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Implementations;
using Newtonsoft.Json;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class Character : IDisposable {
    private const float OrientationCompressionFactor = CharacterHelper.OrientationCompressionFactor;
    [JsonIgnore]
    private readonly byte _defaultUploadIntervalInMinutes = ConfigurationManager.Settings.CharacterUploadIntervalInMinutes;

    public ulong AccountId { get; set; }
    public ulong CharId { get; set; }
    public uint NameIndices { get; set; }
    public WideByteString NameOverride { get; set; }
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
    public WizardSchool WizardSchool { get; set; }
    public WizGameStats GameStats { get; set; }

    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;

    // Empty constructor for deserialization.
    [JsonConstructor] public Character() { }

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

    public void Dispose() {
        // If this object is being disposed, the player probably left the server.
        // Save the character's location to the database.
        CharacterCollection.UpdateCharacterLocation(this, Location, Orientation.Z);
    }
}
