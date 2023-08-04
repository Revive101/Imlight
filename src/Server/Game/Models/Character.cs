/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Globalization;
using Imlight.Common.Utilities;
using Imlight.Server.Data.Statistics;
using SharpDX;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Models;

public class Character
{
    public GID AccountId { get; private set; }
    public GID CharId { get; private set; }
    public uint NameIndices { get; private set; }
    public WideByteString NameOverride { get; private set; }
    public WizardSchool WizardSchool { get; private set; }
    public int Level { get; private set; }
    public string Zone { get; private set; }
    public byte World { get; private set; }
    public int Health { get; private set; }
    public int Mana { get; private set; }
    public int Gold { get; private set; }
    public int Experience { get; private set; }
    public Vector3 Location { get; private set; }
    public Vector3 Orientation { get; private set; }
    public WizardCharacterBehavior WizardAvatar { get; private set; }

    public string LastGameServerIp;
    public ushort LastGameServerPort;
    public string QueuedZoneName;
    public Vector4 QueuedZoneLocation;
    
    // ctor
    public Character(WizardCharacterCreationInfo characterCreationInfo, ulong accountId)
    {
        // Set the account ID and character ID.
        this.AccountId = (GID)accountId;
        this.CharId = RandomGen.GenerateGUID();
        
        // If this constructor has been called, then the character is a fresh character.
        // First, we'll set the character's stats from the creation info.
        this.Level = WorldStats.StartingLevel;
        this.Zone = WorldStats.StartingZone;
        this.World = WorldStats.StartingWorld;
        this.WizardSchool = WizardSchool.Fire; // Need to find these.
        this.WizardAvatar = characterCreationInfo.m_avatarBehavior;
        this.NameIndices = characterCreationInfo.m_nameIndices;
    }

    public void SetLocation(Vector3 loc) 
        => this.Location = loc;

    public void SetLocation(float x, float y, float z) 
        => this.Location = new Vector3(x, y, z);

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
}