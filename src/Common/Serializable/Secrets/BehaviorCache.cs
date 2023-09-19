/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Serializable.ObjectProperty;

namespace Imlight.Common.Serializable.Secrets;

public static class BehaviorCache
{
    public static TypeCache.BehaviorInstance AllocateBehavior(uint hash)
    {
        switch (hash)
        {
            // This list is updated as we go down and learn what each behavior does.

            // ======================================================================
            // PLAYER BEHAVIORS
            // ======================================================================
            case 0x20DFFCEA: return new TypeCache.WizardCharacterBehavior();     // Avatar details, such as skin tone, eye color, etc.
            case 0x1FDBA52F: return new TypeCache.ClientWizPlayerNameBehavior(); // The name above their head.
            case 0x065D0C7A: return new TypeCache.FidgetBehavior();              // How often this character uses an idle animation.
            case 0x2688349D: return new TypeCache.AnimationBehavior();           // Self explanatory.
            case 0x1552CB31: return new TypeCache.ClientMagicSchoolBehavior();   // The school of magic they're in.
            case 0x64FE8AF9: return new TypeCache.ClientSpellbookBehavior();     // The cards in their spellbook.
            case 0x4867764C: return new TypeCache.ClientWizEquipmentBehavior();  // The gear they're wearing.
            case 0x1B610937: return new TypeCache.ClientWizInventoryBehavior();  // The items in their backpack.
                
            // Surprisingly, this has no relation to core emotes.
            case 0x2091D4C9: return new TypeCache.CustomEmoteBehavior();
                
            // ======================================================================
            // WIP. Dealing with zone objects
            // ======================================================================
            // ObjectStateBehavior must be loaded alongside NPCBehavior. Otherwise, the game crashes.
            case 0x28CE8984: return new TypeCache.ObjectStateBehavior();
            case 0x2AFFDE72: return new TypeCache.NPCBehavior();
            case 0x29E13F05: return new TypeCache.EffectsBehavior();
            case 0x0A5CA5EE: return new TypeCache.RenderBehavior();
            case 0x2808A0BF: return new TypeCache.CollisionBehaviorClient();
            case 0x605C502C: return new BasicMobileBehavior();
            case 0x0C46067D: return new TypeCache.BaseGameEffectBehavior();
            case 0x00A6C343: return new TypeCache.ClientInventoryBehavior();
            case 0x1DC10F47: return new TypeCache.ClientEquipmentBehavior();

            // ======================================================================
            // Below are the list of player behaviors we know works.
            // ======================================================================
            // case 0x10393720: return new AdvPvPEloBehavior(); hmm.. removed?
            case 0x70F90C5D: return new TypeCache.CastleToursBehavior();
            case 0x647855BD: return new TypeCache.ClientAlchemyBehavior();
            case 0x06BB59F5: return new TypeCache.ClientAtticBehavior();
            case 0x26FCE31F: return new TypeCache.ClientDynaModBehavior();
            case 0x287E5259: return new TypeCache.ClientExpansionBehavior();
            case 0x1301F271: return new TypeCache.ClientMinigameBehavior();
            case 0x336907AE: return new TypeCache.ClientMountOwnerBehavior();
            case 0x7696F807: return new TypeCache.ClientMountRiderBehavior();
            case 0x0752DB8F: return new TypeCache.ClientPetSnackBehavior();
            case 0x2BD79B75: return new TypeCache.ClientTreasureBookBehavior();
            case 0x1D47EAE3: return new TypeCache.FishingBehavior();
            case 0x1A22DBF4: return new TypeCache.HiddenQuestsBehavior();
            case 0x0A5C8D74: return new TypeCache.LadderBehavior();
            case 0x3AB00B11: return new TypeCache.MonsterMagicBehavior();
            case 0x64E6088B: return new TypeCache.PetTomeBehavior();
            case 0x29474BCF: return new TypeCache.TutorialLogBehavior();
            case 0x174F46F4: return new TypeCache.WishlistBehavior();

            // Client also has PathMovementBehaviorTemplate, but doesn't appear to use the actual behavior.
            //case 0x22B1AD8D: return new PathMovementBehavior();

            // ======================================================================
            // Below are behaviors the client fails on. Unsure as to why.
            // ======================================================================
            //case 0x673324BA: return new ClientPetOwnerBehavior();
            //case 0x06060FD8: return new GamebryoClientLeashBehavior();
            //case 0x2A34C1B2: return new ClientInfractionBehavior();

            // ======================================================================
            // Below are untested behaviors.
            // ======================================================================
            case 0x3E39B11A: return new TypeCache.AccousticAreaBehavior();
            case 0x51CC8A54: return new TypeCache.AmbientSoundBehavior();
            case 0x695422D5: return new TypeCache.AquariumBehavior();
            case 0x6A6CCCD8: return new TypeCache.BreadCrumbBehavior();
            case 0x1F608835: return new TypeCache.CastleBlockDoorBehavior();
            case 0x333B73CB: return new TypeCache.CastleBlocksBehavior();
            case 0x4F040BD7: return new TypeCache.CastleGamesBehavior();
            case 0x30BA7E2B: return new TypeCache.CastleMagicBehavior();
            case 0x667C7023: return new TypeCache.CastleToursFavoritesBehavior();
            case 0x726EAF55: return new TypeCache.CinematicActorBehavior();
            case 0x486338EC: return new TypeCache.ClientBGPlayerBehavior();
            case 0x01CE0AC0: return new TypeCache.ClientBGPolymorphSelectBehavior();
            case 0x51361E0F: return new TypeCache.ClientBGSigilProxyBehavior();
            case 0x78263B80: return new TypeCache.ClientCountdownBehavior();
            case 0x3955A7E3: return new TypeCache.ClientDeckBehavior();
            case 0x0A92B8DB: return new TypeCache.ClientElixirBehavior();
            case 0x14516A7E: return new TypeCache.ClientElixirBenefitBehavior();
            case 0x5FCFCEE4: return new TypeCache.ClientFishBehavior();
            case 0x257E5529: return new TypeCache.ClientGameEffectTimerDisplayBehavior();
            case 0x5CEF62AC: return new TypeCache.ClientJewelSocketBehavior();
            case 0x2DF06F9E: return new TypeCache.ClientObjectRemapBehavior();
            case 0x640F888B: return new TypeCache.ClientPetGameBehavior();
            case 0x75588889: return new TypeCache.ClientPetItemBehavior();
            case 0x640B088B: return new TypeCache.ClientPetNameBehavior();
            case 0x2933F2E5: return new TypeCache.ClientQuantityBehavior();
            case 0x3614A5EE: return new TypeCache.ClientRentalBehavior();
            case 0x346B28D0: return new TypeCache.ClientSpellCardAttachmentBehavior();
            case 0x22FD694B: return new TypeCache.ClientTextureRemapBehavior();
            case 0x6323D8C1: return new TypeCache.ClientTimedItemBehavior();
            case 0x6EF34743: return new TypeCache.ClientWizStorageBehavior();
            case 0x4FACBA18: return new TypeCache.ConicalSoundBehavior();
            case 0x5FC99762: return new TypeCache.DeedBehavior();
            case 0x2D8192F5: return new TypeCache.EquivalentItemBehavior();
            case 0x4375B93E: return new TypeCache.ExtraHousingZoneBehavior();
            case 0x6FF78EFB: return new TypeCache.FurnitureInfoBehavior();
            case 0x12CCD073: return new TypeCache.GardeningBehavior();
            case 0x15563163: return new TypeCache.GardeningShedBehavior();
            case 0x0BF78FE6: return new TypeCache.GearVaultBehavior();
            case 0x1601D1F0: return new TypeCache.GroundContourBehavior();
            case 0x0FC9B7A5: return new TypeCache.HatchmakingKioskBehavior();
            case 0x6D62F311: return new TypeCache.HousingMusicBehavior();
            case 0x774C37C2: return new TypeCache.HousingMusicPlayerBehavior();
            case 0x1C30627D: return new TypeCache.HousingPaletteBehavior();
            case 0x0F48BBA1: return new TypeCache.HousingPetBehavior();
            case 0x6AE06FE1: return new TypeCache.HousingSigilBehavior();
            case 0x5923D594: return new TypeCache.HousingSignBehavior();
            case 0x3E203314: return new TypeCache.HousingTeleporterBehavior();
            case 0x7CF6E65D: return new TypeCache.HousingTextureBehavior();
            case 0x5561F005: return new TypeCache.InteractiveMusicBehavior();
            case 0x47B73B64: return new TypeCache.ItemFinderBehavior();
            case 0x7F13B8CC: return new TypeCache.JewelVaultBehavior();
            case 0x64DA1F0C: return new TypeCache.LeashedPathMovementBehaviorClient();
            case 0x618186AD: return new TypeCache.LinearSoundBehavior();
            case 0x05899C95: return new TypeCache.MobMonsterMagicBehavior();
            case 0x3BAC5871: return new TypeCache.MonsterArenaBehavior();
            case 0x62263818: return new TypeCache.MountItemBehavior();
            case 0x0B6BCC70: return new TypeCache.MountSoundBehavior();
            case 0x52EA8495: return new TypeCache.MoveBehaviorClient();
            case 0x38907C20: return new TypeCache.ObstacleCourseCatapultBehaviorClient();
            case 0x23996AEC: return new TypeCache.ObstacleCourseFinishLineBehaviorClient();
            case 0x086C665C: return new TypeCache.ObstacleCourseModifyTimeBehaviorClient();
            case 0x53D59484: return new TypeCache.ObstacleCoursePendulumBehaviorClient();
            case 0x0CD27E1A: return new TypeCache.ObstacleCoursePusherBehaviorClient();
            case 0x52C907F5: return new TypeCache.ObstacleCourseRevolvingDoorBehaviorClient();
            case 0x7E2AF152: return new TypeCache.ObstacleCourseSpeedUpBehaviorClient();
            case 0x477EE659: return new TypeCache.ObstacleCourseSpringboardBehaviorClient();
            case 0x5FCFD3F6: return new TypeCache.PathBehaviorClient();
            case 0x5FC1CF76: return new TypeCache.PestBehavior();
            case 0x4EFBA675: return new TypeCache.PetDerbyObstacleBehaviorClient();
            case 0x074A42D2: return new TypeCache.PhysicsBehaviorClient();
            case 0x2D701D90: return new TypeCache.PositionalSoundBehavior();
            case 0x2B542F69: return new TypeCache.PositionalStateSoundBehavior();
            case 0x660CC909: return new TypeCache.RidableBehavior();
            case 0x070A552D: return new TypeCache.ScriptBehavior();
            case 0x5FC99777: return new TypeCache.SeedBehavior();
            case 0x2421EE88: return new TypeCache.StatePositionalSoundBehavior();
            case 0x199B6F35: return new TypeCache.TeleportProximityBehavior();
            case 0x030B2DBD: return new TypeCache.TreasureCardPosterBehavior();
            case 0x2C2D02BC: return new TypeCache.TreasureCardVaultBehavior();
            case 0x372E5D42: return new TypeCache.WhirlyBurlyBehavior();
            case 0x1806E68C: return new TypeCache.WhirlyBurlyKioskBehavior();
            case 0x5FCD9562: return new TypeCache.WizardClientDuelBehavior();
            default: return null;
        }
    }

    public class PathMovementBehavior : TypeCache.BehaviorInstance
    {
        public override uint GetHash() => 582069645;

        [Property(769135219, 7)] public Single m_movementSpeed;
        [Property(768663914, 7)] public Single m_movementScale;
    }

    public class BasicMobileBehavior : TypeCache.BehaviorInstance
    {
        public override uint GetHash() => 1616662572;
    }
}