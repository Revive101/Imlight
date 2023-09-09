/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Shared.Secrets
{
    public static class BehaviorCache
    {
        public static BehaviorInstance AllocateBehavior(uint hash)
        {
            switch (hash)
            {
                // This list is updated as we go down and learn what each behavior does.

                // ======================================================================
                // PLAYER BEHAVIORS
                // ======================================================================
                case 0x20DFFCEA: return new WizardCharacterBehavior();     // Avatar details, such as skin tone, eye color, etc.
                case 0x1FDBA52F: return new ClientWizPlayerNameBehavior(); // The name above their head.
                case 0x065D0C7A: return new FidgetBehavior();              // How often this character uses an idle animation.
                case 0x2688349D: return new AnimationBehavior();           // Self explanatory.
                case 0x1552CB31: return new ClientMagicSchoolBehavior();   // The school of magic they're in.
                case 0x64FE8AF9: return new ClientSpellbookBehavior();     // The cards in their spellbook.
                case 0x4867764C: return new ClientWizEquipmentBehavior();  // The gear they're wearing.
                case 0x1B610937: return new ClientWizInventoryBehavior();  // The items in their backpack.
                
                // Surprisingly, this has no relation to core emotes.
                //case 0x2091D4C9: return new CustomEmoteBehavior();
                
                // ======================================================================
                // WIP. Dealing with zone objects
                // ======================================================================
                // ObjectStateBehavior must be loaded alongside NPCBehavior. Otherwise, the game crashes.
                case 0x28CE8984: return new ObjectStateBehavior();
                case 0x2AFFDE72: return new NPCBehavior();
                case 0x29E13F05: return new EffectsBehavior();
                case 0x0A5CA5EE: return new RenderBehavior();
                case 0x2808A0BF: return new CollisionBehaviorClient();
                case 0x605C502C: return new BasicMobileBehavior();
                case 0x0C46067D: return new BaseGameEffectBehavior();
                case 0x00A6C343: return new ClientInventoryBehavior();
                case 0x1DC10F47: return new ClientEquipmentBehavior();

                // ======================================================================
                // Below are the list of player behaviors we know works.
                // ======================================================================
                case 0x10393720: return new AdvPvPEloBehavior();
                case 0x70F90C5D: return new CastleToursBehavior();
                case 0x647855BD: return new ClientAlchemyBehavior();
                case 0x06BB59F5: return new ClientAtticBehavior();
                case 0x26FCE31F: return new ClientDynaModBehavior();
                case 0x287E5259: return new ClientExpansionBehavior();
                case 0x1301F271: return new ClientMinigameBehavior();
                case 0x336907AE: return new ClientMountOwnerBehavior();
                case 0x7696F807: return new ClientMountRiderBehavior();
                case 0x0752DB8F: return new ClientPetSnackBehavior();
                case 0x2BD79B75: return new ClientTreasureBookBehavior();
                case 0x1D47EAE3: return new FishingBehavior();
                case 0x1A22DBF4: return new HiddenQuestsBehavior();
                case 0x0A5C8D74: return new LadderBehavior();
                case 0x3AB00B11: return new MonsterMagicBehavior();
                case 0x64E6088B: return new PetTomeBehavior();
                case 0x29474BCF: return new TutorialLogBehavior();
                case 0x174F46F4: return new WishlistBehavior();

                // Client also has PathMovementBehaviorTemplate, but doesn't appear to use the actual behavior.
                case 0x22B1AD8D: return new PathMovementBehavior();

                // ======================================================================
                // Below are behaviors the client fails on. Unsure as to why.
                // ======================================================================
                //case 0x673324BA: return new ClientPetOwnerBehavior();
                //case 0x06060FD8: return new GamebryoClientLeashBehavior();
                //case 0x2A34C1B2: return new ClientInfractionBehavior();

                // ======================================================================
                // Below are untested behaviors.
                // ======================================================================
                case 0x3E39B11A: return new AccousticAreaBehavior();
                case 0x51CC8A54: return new AmbientSoundBehavior();
                case 0x695422D5: return new AquariumBehavior();
                case 0x6A6CCCD8: return new BreadCrumbBehavior();
                case 0x1F608835: return new CastleBlockDoorBehavior();
                case 0x333B73CB: return new CastleBlocksBehavior();
                case 0x4F040BD7: return new CastleGamesBehavior();
                case 0x30BA7E2B: return new CastleMagicBehavior();
                case 0x667C7023: return new CastleToursFavoritesBehavior();
                case 0x726EAF55: return new CinematicActorBehavior();
                case 0x486338EC: return new ClientBGPlayerBehavior();
                case 0x01CE0AC0: return new ClientBGPolymorphSelectBehavior();
                case 0x51361E0F: return new ClientBGSigilProxyBehavior();
                case 0x78263B80: return new ClientCountdownBehavior();
                case 0x3955A7E3: return new ClientDeckBehavior();
                case 0x0A92B8DB: return new ClientElixirBehavior();
                case 0x14516A7E: return new ClientElixirBenefitBehavior();
                case 0x5FCFCEE4: return new ClientFishBehavior();
                case 0x257E5529: return new ClientGameEffectTimerDisplayBehavior();
                case 0x5CEF62AC: return new ClientJewelSocketBehavior();
                case 0x2DF06F9E: return new ClientObjectRemapBehavior();
                case 0x640F888B: return new ClientPetGameBehavior();
                case 0x75588889: return new ClientPetItemBehavior();
                case 0x640B088B: return new ClientPetNameBehavior();
                case 0x2933F2E5: return new ClientQuantityBehavior();
                case 0x3614A5EE: return new ClientRentalBehavior();
                case 0x346B28D0: return new ClientSpellCardAttachmentBehavior();
                case 0x22FD694B: return new ClientTextureRemapBehavior();
                case 0x6323D8C1: return new ClientTimedItemBehavior();
                case 0x6EF34743: return new ClientWizStorageBehavior();
                case 0x4FACBA18: return new ConicalSoundBehavior();
                case 0x5FC99762: return new DeedBehavior();
                case 0x2D8192F5: return new EquivalentItemBehavior();
                case 0x4375B93E: return new ExtraHousingZoneBehavior();
                case 0x6FF78EFB: return new FurnitureInfoBehavior();
                case 0x12CCD073: return new GardeningBehavior();
                case 0x15563163: return new GardeningShedBehavior();
                case 0x0BF78FE6: return new GearVaultBehavior();
                case 0x1601D1F0: return new GroundContourBehavior();
                case 0x0FC9B7A5: return new HatchmakingKioskBehavior();
                case 0x6D62F311: return new HousingMusicBehavior();
                case 0x774C37C2: return new HousingMusicPlayerBehavior();
                case 0x1C30627D: return new HousingPaletteBehavior();
                case 0x0F48BBA1: return new HousingPetBehavior();
                case 0x6AE06FE1: return new HousingSigilBehavior();
                case 0x5923D594: return new HousingSignBehavior();
                case 0x3E203314: return new HousingTeleporterBehavior();
                case 0x7CF6E65D: return new HousingTextureBehavior();
                case 0x5561F005: return new InteractiveMusicBehavior();
                case 0x47B73B64: return new ItemFinderBehavior();
                case 0x7F13B8CC: return new JewelVaultBehavior();
                case 0x64DA1F0C: return new LeashedPathMovementBehaviorClient();
                case 0x618186AD: return new LinearSoundBehavior();
                case 0x05899C95: return new MobMonsterMagicBehavior();
                case 0x3BAC5871: return new MonsterArenaBehavior();
                case 0x62263818: return new MountItemBehavior();
                case 0x0B6BCC70: return new MountSoundBehavior();
                case 0x52EA8495: return new MoveBehaviorClient();
                case 0x38907C20: return new ObstacleCourseCatapultBehaviorClient();
                case 0x23996AEC: return new ObstacleCourseFinishLineBehaviorClient();
                case 0x086C665C: return new ObstacleCourseModifyTimeBehaviorClient();
                case 0x53D59484: return new ObstacleCoursePendulumBehaviorClient();
                case 0x0CD27E1A: return new ObstacleCoursePusherBehaviorClient();
                case 0x52C907F5: return new ObstacleCourseRevolvingDoorBehaviorClient();
                case 0x7E2AF152: return new ObstacleCourseSpeedUpBehaviorClient();
                case 0x477EE659: return new ObstacleCourseSpringboardBehaviorClient();
                case 0x5FCFD3F6: return new PathBehaviorClient();
                case 0x5FC1CF76: return new PestBehavior();
                case 0x4EFBA675: return new PetDerbyObstacleBehaviorClient();
                case 0x074A42D2: return new PhysicsBehaviorClient();
                case 0x2D701D90: return new PositionalSoundBehavior();
                case 0x2B542F69: return new PositionalStateSoundBehavior();
                case 0x660CC909: return new RidableBehavior();
                case 0x070A552D: return new ScriptBehavior();
                case 0x5FC99777: return new SeedBehavior();
                case 0x2421EE88: return new StatePositionalSoundBehavior();
                case 0x199B6F35: return new TeleportProximityBehavior();
                case 0x030B2DBD: return new TreasureCardPosterBehavior();
                case 0x2C2D02BC: return new TreasureCardVaultBehavior();
                case 0x372E5D42: return new WhirlyBurlyBehavior();
                case 0x1806E68C: return new WhirlyBurlyKioskBehavior();
                case 0x5FCD9562: return new WizardClientDuelBehavior();
                default: return null;
            }
        }

        public class PathMovementBehavior : BehaviorInstance
        {
            public override uint GetHash() => 582069645;

            [Property(769135219, 7)] public Single m_movementSpeed;
            [Property(768663914, 7)] public Single m_movementScale;
        }

        public class BasicMobileBehavior : BehaviorInstance
        {
            public override uint GetHash() => 1616662572;
        }
    }
}
