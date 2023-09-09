/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Imlight.Common.Configuration;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Resources;
using Imlight.Server.WizardData;
using Imlight.Server.WizardData.Implementations;
using Imlight.Server.WizardData.Models;
using Newtonsoft.Json;
using SharpDX;
using WizUnraveler.Cache;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static Imlight.Server.WizardData.WizardResults;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Models;

[Serializable]
public class Character : IDisposable
{
    [JsonIgnore] private readonly byte _defaultUploadIntervalInMinutes
        = ConfigurationManager.Settings.CharacterUploadIntervalInMinutes;
    
    public ulong AccountId { get; set; }
    public ulong CharId { get; init; }
    public WizardCharacterBehavior WizardAvatar { get; init; }
    public uint NameIndices { get; init; }
    public WideByteString NameOverride { get; init; }
    public WizardSchool WizardSchool { get; init; }
    public int Level { get; private set; }
    public string Zone { get; private set; }
    public byte World { get; private set; }
    public WizGameStats GameStats { get; private set; }
    public int TrainingPoints { get; set; }
    public int XpToNextLevel { get; set; }
    public bool IsVolunteer { get; private set; }
    public string MarkedZoneName { get; private set; }
    public Vector3 MarkedLocation { get; private set; }
    public Vector3 MarkedLocationOrientation { get; private set; }
    public Vector3 Location
    {
        get => this.GameObject?.m_location ?? _location;
        set
        {
            if (this.GameObject is not null) this.GameObject.m_location = value;
            else _location = value;
        }
    }
    public Vector3 Orientation
    {
        get => this.GameObject?.m_orientation ?? _orientation;
        set
        {
            if (this.GameObject is not null) this.GameObject.m_orientation = value;
            else _orientation = value;
        }
    }
    
    [JsonIgnore] public WizClientObject GameObject;
    [JsonIgnore] public string GameServerIp;
    [JsonIgnore] public ushort GameServerPort;
    [JsonIgnore] public string QueuedZoneName;
    [JsonIgnore] public string QueuedZoneLocation;

    [JsonIgnore] private Vector3 _location;
    [JsonIgnore] private Vector3 _orientation;
    [JsonIgnore] private Dictionary<Type, MethodInfo> _resultHandlers;
    [JsonIgnore] private ElementChangeCacheManager _cacheManager;
    
    // Empty constructor for deserialization.
    [JsonConstructor] public Character() {}

    // ctor
    public Character(WizardCharacterCreationInfo characterCreationInfo)
    {
        // If this constructor has been called, then the character is a fresh character.
        this.CharId = RandomGen.GenerateGUID();
        this.Level = ConfigurationManager.Settings.StartingLevel;
        this.Zone = ConfigurationManager.Settings.StartingZone;
        this.World = ConfigurationManager.Settings.StartingWorld;
        this.WizardSchool = (WizardSchool)characterCreationInfo.m_schoolOfFocus;
        this.WizardAvatar = characterCreationInfo.m_avatarBehavior;
        this.NameIndices = characterCreationInfo.m_nameIndices;

        // Create the game stats and calculate the base stats.
        var gameStats = new WizGameStats();
        gameStats = CalculateBaseGameStats(gameStats);
        this.GameStats = gameStats;

        SetResultHandlers();
    }

    public void SetLocation(Vector3 loc)
    {
        this.Location = loc;
        
        SendCachedChange(nameof(Location), 10, loc);
    }

    public void SetLocation(string loc)
    {
        // The only time a string location is given is on attach. In which case, we want to disassemble the string
        // and immediately persist the location change.
        var location = Util.GetVectorFromCompactString(loc);
        this.Location = new Vector3(location.X, location.Y, location.Z);
        this.Orientation = new Vector3(0, 0, location.W);

        SendPersistentChange(nameof(Location), this.Location);
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
        SendPersistentChange(nameof(Zone), zone);
    }
    
    public void SetMarkedLocation(Vector3 loc, Vector3 orientation, string zoneName)
    {
        if (zoneName != this.Zone)
        {
            Log.Error($"Character tried to set a marker in a zone ({0}) in a zone it wasn't in {1}",
                Log.Args(zoneName, this.Zone));
            return;
        }

        this.MarkedLocation = loc;
        this.MarkedLocationOrientation = orientation;
        this.MarkedZoneName = zoneName;
        SendPersistentChange(nameof(MarkedLocation), loc);
    }

    public string GetStringLocation()
    {
        // If the location is zero, return "Start."
        return this.Location == Vector3.Zero 
            ? "Start" 
            : Util.GetCompactStringFromVector(this.Location, this.Orientation);
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
            m_globalID = (GID)this.CharId,
            m_templateID = 1,
            m_userID = (GID)this.AccountId,
            // TODO: Equipment list
        };
        return creationInfo;
    }

    public void HandleResult(TypeCache.Result result)
    {
        // Find the method that handles this message type
        if (_resultHandlers.TryGetValue(result.GetType(), out var method))
        {
            // Invoke the method with the message
            method.Invoke(this, new object[] { result });
        }
        else
        {
            Log.Warning("No character result handler for result type {ResultType}.", Log.Args(result.GetType()));
        }
    }
    
    public void Dispose()
    {
        FlushPersistentChanges();
        _cacheManager?.Dispose();
    }

    private WizGameStats CalculateBaseGameStats(WizGameStats existingStats)
    {
        var baseHealth = WizardClassData.GetClassHealthAtLevel(WizardSchool, Level);
        var baseMana = WizardClassData.GetManaAtLevel(Level);

        existingStats.m_baseHitpoints = baseHealth;
        existingStats.m_currentHitpoints = baseHealth;
        existingStats.m_baseMana = baseMana;
        existingStats.m_currentMana = baseMana;
        existingStats.m_baseGoldPouch = ConfigurationManager.Settings.BaseGoldPouch;
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
    
    private void SetResultHandlers()
    {
        _resultHandlers = new Dictionary<System.Type, MethodInfo>();

        // Get all methods in this actor with a message handling attribute
        var methods = this
            .GetType()
            .GetMethods(BindingFlags.Instance 
                        | BindingFlags.Public 
                        | BindingFlags.NonPublic
                        | BindingFlags.FlattenHierarchy)
            .Where(method => method.GetCustomAttributes<ResultHandlerAttribute>().Any());

        foreach (var method in methods)
        {
            var paramType = method.GetParameters()[0].ParameterType;
            _resultHandlers.Add(paramType, method);
        }
    }

    private void SendCachedChange<T>(string elementName, byte batchSize, T value)
    {
        _cacheManager ??= new ElementChangeCacheManager(PlayerDatabase.Instance.Store, CharId, _defaultUploadIntervalInMinutes);
        _cacheManager.EnqueueChange(elementName, value);
    }
    
    private void SendPersistentChange<T>(string elementName, T value)
    {
        _cacheManager ??= new ElementChangeCacheManager(PlayerDatabase.Instance.Store, CharId, _defaultUploadIntervalInMinutes);
        _cacheManager.EnqueueImmediateChange(elementName, value);
    }
    
    public void FlushPersistentChanges()
    {
        _cacheManager?.FlushAllChangesAsync().RunSynchronously();
    }
    
    #region Result Handlers

    [ResultHandler(typeof(ResAddHealth))]
    private void ReceiveAddHealth(ResAddHealth result)
    {
        // If we're using flat health, just add the health up to the max health.
        if (result.UseFlat)
        {
            this.GameStats.m_currentHitpoints += result.HealthFlat;
            if (this.GameStats.m_currentHitpoints > this.GameStats.m_baseHitpoints)
            {
                this.GameStats.m_currentHitpoints = this.GameStats.m_baseHitpoints;
            }
        }
        else
        {
            // If we're using percent health, add the percent of the max health.
            var percent = result.HealthPercent / 100f;
            var amount = this.GameStats.m_baseHitpoints * percent;
            this.GameStats.m_currentHitpoints += (int)amount;
            if (this.GameStats.m_currentHitpoints > this.GameStats.m_baseHitpoints)
            {
                this.GameStats.m_currentHitpoints = this.GameStats.m_baseHitpoints;
            }
        }
    }
    
    #endregion
}