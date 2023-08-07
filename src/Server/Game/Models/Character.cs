/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Imlight.Common.Utilities;
using Imlight.Server.Data;
using Imlight.Server.Data.WizardData;
using SharpDX;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Models;

public class Character
{
    public GID AccountId { get; init; }
    public GID CharId { get; init; }
    public WizardCharacterBehavior WizardAvatar { get; init; }
    public uint NameIndices { get; init; }
    public WideByteString NameOverride { get; init; }
    public WizardSchool WizardSchool { get; init; }
    public int Level { get; private set; }
    public string Zone { get; private set; }
    public byte World { get; private set; }
    public WizGameStats GameStats { get; private set; }
    public int TrainingPoints { get; private set; }
    public int XpToNextLevel { get; private set; }
    public bool IsVolunteer { get; private set; }
    public Vector3 Location
    {
        get => this.GameObject?.m_location ?? Vector3.Zero;
        set
        {
            if (this.GameObject is not null) this.GameObject.m_location = value;
        }
    }
    public Vector3 Orientation
    {
        get => this.GameObject?.m_orientation ?? Vector3.Zero;
        set
        {
            if (this.GameObject is not null) this.GameObject.m_orientation = value;
        }
    }

    // These stats should not be saved to the database.
    public WizClientObject GameObject;
    public string LastGameServerIp;
    public ushort LastGameServerPort;
    public string QueuedZoneName;
    public string QueuedZoneLocation;

    // ctor
    public Character(WizardCharacterCreationInfo characterCreationInfo, ulong accountId)
    {
        // Set the account ID and character ID.
        this.AccountId = (GID)accountId;
        this.CharId = RandomGen.GenerateGUID();
        
        // If this constructor has been called, then the character is a fresh character.
        // First, we'll set the character's stats from the creation info.
        this.Level = WizardWorldData.StartingLevel;
        this.Zone = WizardWorldData.StartingZone;
        this.World = WizardWorldData.StartingWorld;
        this.WizardSchool = (WizardSchool)characterCreationInfo.m_schoolOfFocus;
        this.WizardAvatar = characterCreationInfo.m_avatarBehavior;
        this.NameIndices = characterCreationInfo.m_nameIndices;

        // Create the game stats and calculate the base stats.
        var gameStats = new WizGameStats();
        gameStats = CalculateBaseGameStats(gameStats);
        this.GameStats = gameStats;
    }

    public void SetLocation(Vector3 loc) 
        => this.Location = loc;

    public void SetLocation(float x, float y, float z) 
        => this.Location = new Vector3(x, y, z);

    public void SetLocation(string loc)
    {
        var location = Util.GetVectorFromCompactString(loc);
        this.Location = new Vector3(location.X, location.Y, location.Z);
        this.Orientation = new Vector3(0, 0, location.W);
    }

    public string GetStringLocation()
    {
        // If the location is zero, return "Start."
        return this.Location == Vector3.Zero 
            ? "Start" 
            : Data.Util.GetCompactStringFromVector(this.Location, this.Orientation);
    }

    public void SetZone(string zone)
    {
        // Check if the zone exists in the AccessPass.
        if (!AccessPassManager.DoesZoneExist(zone))
        {
            Log.Error("Character tried to set itself to zone {Zone}, but that zone does not exist.", 
                Log.Args(zone));
            return;
        }
        
        this.Zone = zone;
    }

    public WizardCharacterCreationInfo GetCharacterCreationInfo()
    {
        var creationInfo = new WizardCharacterCreationInfo
        {
            m_avatarBehavior = this.WizardAvatar,
            m_nameIndices = this.NameIndices,
            m_schoolOfFocus = (uint)this.WizardSchool,
            m_level = this.Level,
            m_name = this.NameOverride,
            m_world = this.World,
            m_location = this.Zone,
            m_globalID = this.CharId,
            m_templateID = 1,
            m_userID = this.AccountId,
            // TODO: Equipment list
        };
        return creationInfo;
    }

    private WizGameStats CalculateBaseGameStats(WizGameStats existingStats)
    {
        var baseHealth = WizardClassData.GetClassHealthAtLevel(WizardSchool, Level);
        var baseMana = WizardClassData.GetManaAtLevel(Level);

        existingStats.m_baseHitpoints = baseHealth;
        existingStats.m_currentHitpoints = baseHealth;
        existingStats.m_baseMana = baseMana;
        existingStats.m_currentMana = baseMana;
        existingStats.m_baseGoldPouch = WizardWorldData.GoldPouchMax;
        existingStats.m_powerPipBase = WizardClassData.GetPowerPipChanceAtLevel(Level);
        existingStats.m_energyMax = WizardClassData.GetPetEnergyAtLevel(Level);
        
        // Initialize the lists.
        existingStats.m_blockPercentBySchool = new List<float>();
        existingStats.m_blockRatingBySchool = new List<float>();
        existingStats.m_dmgBonusFlat = new List<float>();
        existingStats.m_dmgBonusPercent = new List<float>();
        existingStats.m_dmgBonusFlat = new List<float>();
        existingStats.m_dmgReduceFlat = new List<float>();
        existingStats.m_dmgReducePercent = new List<float>();

        return existingStats;
    }
}