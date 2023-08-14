/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.WizardData;

public static class WizardResults
{
    // TODO: Move the ResTeleport off of WizUnraveler and to this file instead.
    
    public class ResAddHealth : Result
    {
        public int HealthFlat { get; set; }
        public int HealthPercent { get; set; }
        public bool UseFlat { get; set; }
    }
    
    public class ResRemoveHealth : Result
    {
        public int HealthFlat { get; set; }
        public int HealthPercent { get; set; }
        public bool UseFlat { get; set; }
    }

    public class ResAddMana : Result
    {
        public int ManaFlat { get; set; }
        public int ManaPercent { get; set; }
        public bool UseFlat { get; set; }
    }
    
    public class ResRemoveMana : Result
    {
        public int ManaFlat { get; set; }
        public int ManaPercent { get; set; }
        public bool UseFlat { get; set; }
    }
    
    public class ResAddGold : Result
    {
        public int Gold { get; set; }
    }
    
    public class ResRemoveGold : Result
    {
        public int Gold { get; set; }
    }
    
    public class ResAddTrainingPoints : Result
    {
        public int TrainingPoints { get; set; }
    }

    public class ResAddMagicXP : Result
    {
        public uint Experience;
        public ByteString MagicSchool;
        public ByteString SourceType;
    }
    
    public class ResClearSpellbook : Result
    {
        public override uint GetHash() => 0x0;
    }
        
    public class ResGiveSpell : Result
    {
        public ByteString m_spellName;
        public byte m_subCircle;
    }
        
    public class ResSetPips : Result
    {
        public byte m_numPips;
        public byte m_subCircle;
    }
        
    public class ResClearHand : Result
    {

    }
}