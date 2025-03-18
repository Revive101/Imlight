/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */


using Imcodec.ObjectProperty.Attributes;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Shared.Resources;

public static class BehaviorCache {

    public static BehaviorInstance AllocateBehavior(uint hash) => hash switch {
        // This list is updated as we go down and learn what each behavior does.
        // ======================================================================
        // PLAYER BEHAVIORS
        // ======================================================================
        0x20DFFCEA => new WizardCharacterBehavior(),     // Avatar details, such as skin tone, eye color, etc.
        0x1FDBA52F => new ClientWizPlayerNameBehavior(), // The name above their head.
        0x065D0C7A => new FidgetBehavior(),              // How often this character uses an idle animation.
        0x2688349D => new AnimationBehavior(),           // Self explanatory.
        0x1552CB31 => new ClientMagicSchoolBehavior(),   // The school of magic they're in.
        0x64FE8AF9 => new ClientSpellbookBehavior(),     // The cards in their spellbook.
        0x4867764C => new ClientWizEquipmentBehavior(),  // The gear they're wearing.
        0x1B610937 => new ClientWizInventoryBehavior(),  // The items in their backpack.
        0x2091D4C9 => new CustomEmoteBehavior(),         // Surprisingly, this has no relation to core emotes.
        // ======================================================================
        // WIP. Dealing with zone objects
        // ======================================================================
        // ObjectStateBehavior must be loaded alongside NPCBehavior. Otherwise, the game crashes.
        0x28CE8984 => new ObjectStateBehavior(),
        0x2AFFDE72 => new NPCBehavior(),
        0x29E13F05 => new EffectsBehavior(),
        0x0A5CA5EE => new RenderBehavior(),
        0x2808A0BF => new CollisionBehaviorClient(),
        0x605C502C => new BasicMobileBehavior(),
        0x0C46067D => new BaseGameEffectBehavior(),
        0x00A6C343 => new ClientInventoryBehavior(),
        0x1DC10F47 => new ClientEquipmentBehavior(),
        // ======================================================================
        // Below are the list of player behaviors we know works.
        // ======================================================================
        // case 0x10393720: return new AdvPvPEloBehavior(); hmm.. removed?
        0x70F90C5D => new CastleToursBehavior(),
        0x647855BD => new ClientAlchemyBehavior(),
        0x06BB59F5 => new ClientAtticBehavior(),
        0x26FCE31F => new ClientDynaModBehavior(),
        0x287E5259 => new ClientExpansionBehavior(),
        0x1301F271 => new ClientMinigameBehavior(),
        0x336907AE => new ClientMountOwnerBehavior(),
        0x7696F807 => new ClientMountRiderBehavior(),
        0x0752DB8F => new ClientPetSnackBehavior(),
        0x2BD79B75 => new ClientTreasureBookBehavior(),
        0x1D47EAE3 => new FishingBehavior(),
        0x1A22DBF4 => new HiddenQuestsBehavior(),
        0x0A5C8D74 => new LadderBehavior(),
        0x3AB00B11 => new MonsterMagicBehavior(),
        0x64E6088B => new PetTomeBehavior(),
        0x29474BCF => new TutorialLogBehavior(),
        0x174F46F4 => new WishlistBehavior(),
        0x673324BA => new ClientPetOwnerBehavior(),
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
        0x3E39B11A => new AccousticAreaBehavior(),
        0x51CC8A54 => new AmbientSoundBehavior(),
        0x695422D5 => new AquariumBehavior(),
        0x6A6CCCD8 => new BreadCrumbBehavior(),
        0x1F608835 => new CastleBlockDoorBehavior(),
        0x333B73CB => new CastleBlocksBehavior(),
        0x4F040BD7 => new CastleGamesBehavior(),
        0x30BA7E2B => new CastleMagicBehavior(),
        0x667C7023 => new CastleToursFavoritesBehavior(),
        0x726EAF55 => new CinematicActorBehavior(),
        0x486338EC => new ClientBGPlayerBehavior(),
        0x01CE0AC0 => new ClientBGPolymorphSelectBehavior(),
        0x51361E0F => new ClientBGSigilProxyBehavior(),
        0x78263B80 => new ClientCountdownBehavior(),
        0x3955A7E3 => new ClientDeckBehavior(),
        0x0A92B8DB => new ClientElixirBehavior(),
        0x14516A7E => new ClientElixirBenefitBehavior(),
        0x5FCFCEE4 => new ClientFishBehavior(),
        0x257E5529 => new ClientGameEffectTimerDisplayBehavior(),
        0x5CEF62AC => new ClientJewelSocketBehavior(),
        0x2DF06F9E => new ClientObjectRemapBehavior(),
        0x640F888B => new ClientPetGameBehavior(),
        0x75588889 => new ClientPetItemBehavior(),
        0x640B088B => new ClientPetNameBehavior(),
        0x2933F2E5 => new ClientQuantityBehavior(),
        0x3614A5EE => new ClientRentalBehavior(),
        0x346B28D0 => new ClientSpellCardAttachmentBehavior(),
        0x22FD694B => new ClientTextureRemapBehavior(),
        0x6323D8C1 => new ClientTimedItemBehavior(),
        0x6EF34743 => new ClientWizStorageBehavior(),
        0x4FACBA18 => new ConicalSoundBehavior(),
        0x5FC99762 => new DeedBehavior(),
        0x2D8192F5 => new EquivalentItemBehavior(),
        0x4375B93E => new ExtraHousingZoneBehavior(),
        0x6FF78EFB => new FurnitureInfoBehavior(),
        0x12CCD073 => new GardeningBehavior(),
        0x15563163 => new GardeningShedBehavior(),
        0x0BF78FE6 => new GearVaultBehavior(),
        0x1601D1F0 => new GroundContourBehavior(),
        0x0FC9B7A5 => new HatchmakingKioskBehavior(),
        0x6D62F311 => new HousingMusicBehavior(),
        0x774C37C2 => new HousingMusicPlayerBehavior(),
        0x1C30627D => new HousingPaletteBehavior(),
        0x0F48BBA1 => new HousingPetBehavior(),
        0x6AE06FE1 => new HousingSigilBehavior(),
        0x5923D594 => new HousingSignBehavior(),
        0x3E203314 => new HousingTeleporterBehavior(),
        0x7CF6E65D => new HousingTextureBehavior(),
        0x5561F005 => new InteractiveMusicBehavior(),
        0x47B73B64 => new ItemFinderBehavior(),
        0x7F13B8CC => new JewelVaultBehavior(),
        0x64DA1F0C => new LeashedPathMovementBehaviorClient(),
        0x618186AD => new LinearSoundBehavior(),
        0x05899C95 => new MobMonsterMagicBehavior(),
        0x3BAC5871 => new MonsterArenaBehavior(),
        0x62263818 => new MountItemBehavior(),
        0x0B6BCC70 => new MountSoundBehavior(),
        0x52EA8495 => new MoveBehaviorClient(),
        0x38907C20 => new ObstacleCourseCatapultBehaviorClient(),
        0x23996AEC => new ObstacleCourseFinishLineBehaviorClient(),
        0x086C665C => new ObstacleCourseModifyTimeBehaviorClient(),
        0x53D59484 => new ObstacleCoursePendulumBehaviorClient(),
        0x0CD27E1A => new ObstacleCoursePusherBehaviorClient(),
        0x52C907F5 => new ObstacleCourseRevolvingDoorBehaviorClient(),
        0x7E2AF152 => new ObstacleCourseSpeedUpBehaviorClient(),
        0x477EE659 => new ObstacleCourseSpringboardBehaviorClient(),
        0x5FCFD3F6 => new PathBehaviorClient(),
        0x5FC1CF76 => new PestBehavior(),
        0x4EFBA675 => new PetDerbyObstacleBehaviorClient(),
        0x074A42D2 => new PhysicsBehaviorClient(),
        0x2D701D90 => new PositionalSoundBehavior(),
        0x2B542F69 => new PositionalStateSoundBehavior(),
        0x660CC909 => new RidableBehavior(),
        0x070A552D => new ScriptBehavior(),
        0x5FC99777 => new SeedBehavior(),
        0x2421EE88 => new StatePositionalSoundBehavior(),
        0x199B6F35 => new TeleportProximityBehavior(),
        0x030B2DBD => new TreasureCardPosterBehavior(),
        0x2C2D02BC => new TreasureCardVaultBehavior(),
        0x372E5D42 => new WhirlyBurlyBehavior(),
        0x1806E68C => new WhirlyBurlyKioskBehavior(),
        0x5FCD9562 => new WizardClientDuelBehavior(),
        _ => null,
    };

    [PropertySerializationTarget]
    public record PathMovementBehavior : BehaviorInstance {

        public override uint GetHash() => 582069645;

        [PropertyField(769135219, 7)] public float m_movementSpeed { get; set; }
        [PropertyField(768663914, 7)] public float m_movementScale { get; set; }

    }

    [PropertySerializationTarget]
    public record BasicMobileBehavior : BehaviorInstance {

        public override uint GetHash() => 1616662572;

    }

}
