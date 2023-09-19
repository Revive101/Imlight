/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.IO;
using Imlight.Common.Serializable.Caches;

namespace Imlight.Server.Shared.WizardData;

public static class WizardResults
{
    // TODO: Move the ResTeleport off of WizUnraveler and to this file instead.

    public class ResAddHealth : TypeCache.Result
    {
        public int HealthFlat { get; set; }
        public int HealthPercent { get; set; }
        public bool UseFlat { get; set; }
    }

    public class ResRemoveHealth : TypeCache.Result
    {
        public int HealthFlat { get; set; }
        public int HealthPercent { get; set; }
        public bool UseFlat { get; set; }
    }

    public class ResAddMana : TypeCache.Result
    {
        public int ManaFlat { get; set; }
        public int ManaPercent { get; set; }
        public bool UseFlat { get; set; }
    }

    public class ResRemoveMana : TypeCache.Result
    {
        public int ManaFlat { get; set; }
        public int ManaPercent { get; set; }
        public bool UseFlat { get; set; }
    }

    public class ResAddGold : TypeCache.Result
    {
        public int Gold { get; set; }
    }

    public class ResRemoveGold : TypeCache.Result
    {
        public int Gold { get; set; }
    }

    public class ResAddTrainingPoints : TypeCache.Result
    {
        public int TrainingPoints { get; set; }
    }

    public class ResAddMagicXP : TypeCache.Result
    {
        public uint Experience;
        public ByteString MagicSchool;
        public ByteString SourceType;
    }

    public class ResClearSpellbook : TypeCache.Result
    {
        public override uint GetHash() => 0x0;
    }

    public class ResGiveSpell : TypeCache.Result
    {
        public ByteString m_spellName;
        public byte m_subCircle;
    }

    public class ResSetPips : TypeCache.Result
    {
        public byte m_numPips;
        public byte m_subCircle;
    }

    public class ResClearHand : TypeCache.Result
    {

    }
}