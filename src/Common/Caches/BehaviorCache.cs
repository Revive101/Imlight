/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using System;

namespace Imlight.Common.Caches;

public static class BehaviorCache {
    public static TypeCache.BehaviorInstance? AllocateBehavior(uint hash) {
        return hash switch {
            // This list is updated as we go down and learn what each behavior does.
            // ======================================================================
            // PLAYER BEHAVIORS
            // ======================================================================
            0x20DFFCEA => new TypeCache.WizardCharacterBehavior(),// Avatar details, such as skin tone, eye color, etc.
            0x1FDBA52F => new TypeCache.ClientWizPlayerNameBehavior(),// The name above their head.
            0x065D0C7A => new TypeCache.FidgetBehavior(),// How often this character uses an idle animation.
            0x2688349D => new TypeCache.AnimationBehavior(),// Self explanatory.
            0x1552CB31 => new TypeCache.ClientMagicSchoolBehavior(),// The school of magic they're in.
            0x64FE8AF9 => new TypeCache.ClientSpellbookBehavior(),// The cards in their spellbook.
            0x4867764C => new TypeCache.ClientWizEquipmentBehavior(),// The gear they're wearing.
            0x1B610937 => new TypeCache.ClientWizInventoryBehavior(),// The items in their backpack.
                                                                     // Surprisingly, this has no relation to core emotes.
            0x2091D4C9 => new TypeCache.CustomEmoteBehavior(),
            // ======================================================================
            // WIP. Dealing with zone objects
            // ======================================================================
            // ObjectStateBehavior must be loaded alongside NPCBehavior. Otherwise, the game crashes.
            0x28CE8984 => new TypeCache.ObjectStateBehavior(),
            0x2AFFDE72 => new TypeCache.NPCBehavior(),
            0x29E13F05 => new TypeCache.EffectsBehavior(),
            0x0A5CA5EE => new TypeCache.RenderBehavior(),
            0x2808A0BF => new TypeCache.CollisionBehaviorClient(),
            0x605C502C => new BasicMobileBehavior(),
            0x0C46067D => new TypeCache.BaseGameEffectBehavior(),
            0x00A6C343 => new TypeCache.ClientInventoryBehavior(),
            0x1DC10F47 => new TypeCache.ClientEquipmentBehavior(),
            // ======================================================================
            // Below are the list of player behaviors we know works.
            // ======================================================================
            // case 0x10393720: return new AdvPvPEloBehavior(); hmm.. removed?
            0x70F90C5D => new TypeCache.CastleToursBehavior(),
            0x647855BD => new TypeCache.ClientAlchemyBehavior(),
            0x06BB59F5 => new TypeCache.ClientAtticBehavior(),
            0x26FCE31F => new TypeCache.ClientDynaModBehavior(),
            0x287E5259 => new TypeCache.ClientExpansionBehavior(),
            0x1301F271 => new TypeCache.ClientMinigameBehavior(),
            0x336907AE => new TypeCache.ClientMountOwnerBehavior(),
            0x7696F807 => new TypeCache.ClientMountRiderBehavior(),
            0x0752DB8F => new TypeCache.ClientPetSnackBehavior(),
            0x2BD79B75 => new TypeCache.ClientTreasureBookBehavior(),
            0x1D47EAE3 => new TypeCache.FishingBehavior(),
            0x1A22DBF4 => new TypeCache.HiddenQuestsBehavior(),
            0x0A5C8D74 => new TypeCache.LadderBehavior(),
            0x3AB00B11 => new TypeCache.MonsterMagicBehavior(),
            0x64E6088B => new TypeCache.PetTomeBehavior(),
            0x29474BCF => new TypeCache.TutorialLogBehavior(),
            0x174F46F4 => new TypeCache.WishlistBehavior(),
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
            0x3E39B11A => new TypeCache.AccousticAreaBehavior(),
            0x51CC8A54 => new TypeCache.AmbientSoundBehavior(),
            0x695422D5 => new TypeCache.AquariumBehavior(),
            0x6A6CCCD8 => new TypeCache.BreadCrumbBehavior(),
            0x1F608835 => new TypeCache.CastleBlockDoorBehavior(),
            0x333B73CB => new TypeCache.CastleBlocksBehavior(),
            0x4F040BD7 => new TypeCache.CastleGamesBehavior(),
            0x30BA7E2B => new TypeCache.CastleMagicBehavior(),
            0x667C7023 => new TypeCache.CastleToursFavoritesBehavior(),
            0x726EAF55 => new TypeCache.CinematicActorBehavior(),
            0x486338EC => new TypeCache.ClientBGPlayerBehavior(),
            0x01CE0AC0 => new TypeCache.ClientBGPolymorphSelectBehavior(),
            0x51361E0F => new TypeCache.ClientBGSigilProxyBehavior(),
            0x78263B80 => new TypeCache.ClientCountdownBehavior(),
            0x3955A7E3 => new TypeCache.ClientDeckBehavior(),
            0x0A92B8DB => new TypeCache.ClientElixirBehavior(),
            0x14516A7E => new TypeCache.ClientElixirBenefitBehavior(),
            0x5FCFCEE4 => new TypeCache.ClientFishBehavior(),
            0x257E5529 => new TypeCache.ClientGameEffectTimerDisplayBehavior(),
            0x5CEF62AC => new TypeCache.ClientJewelSocketBehavior(),
            0x2DF06F9E => new TypeCache.ClientObjectRemapBehavior(),
            0x640F888B => new TypeCache.ClientPetGameBehavior(),
            0x75588889 => new TypeCache.ClientPetItemBehavior(),
            0x640B088B => new TypeCache.ClientPetNameBehavior(),
            0x2933F2E5 => new TypeCache.ClientQuantityBehavior(),
            0x3614A5EE => new TypeCache.ClientRentalBehavior(),
            0x346B28D0 => new TypeCache.ClientSpellCardAttachmentBehavior(),
            0x22FD694B => new TypeCache.ClientTextureRemapBehavior(),
            0x6323D8C1 => new TypeCache.ClientTimedItemBehavior(),
            0x6EF34743 => new TypeCache.ClientWizStorageBehavior(),
            0x4FACBA18 => new TypeCache.ConicalSoundBehavior(),
            0x5FC99762 => new TypeCache.DeedBehavior(),
            0x2D8192F5 => new TypeCache.EquivalentItemBehavior(),
            0x4375B93E => new TypeCache.ExtraHousingZoneBehavior(),
            0x6FF78EFB => new TypeCache.FurnitureInfoBehavior(),
            0x12CCD073 => new TypeCache.GardeningBehavior(),
            0x15563163 => new TypeCache.GardeningShedBehavior(),
            0x0BF78FE6 => new TypeCache.GearVaultBehavior(),
            0x1601D1F0 => new TypeCache.GroundContourBehavior(),
            0x0FC9B7A5 => new TypeCache.HatchmakingKioskBehavior(),
            0x6D62F311 => new TypeCache.HousingMusicBehavior(),
            0x774C37C2 => new TypeCache.HousingMusicPlayerBehavior(),
            0x1C30627D => new TypeCache.HousingPaletteBehavior(),
            0x0F48BBA1 => new TypeCache.HousingPetBehavior(),
            0x6AE06FE1 => new TypeCache.HousingSigilBehavior(),
            0x5923D594 => new TypeCache.HousingSignBehavior(),
            0x3E203314 => new TypeCache.HousingTeleporterBehavior(),
            0x7CF6E65D => new TypeCache.HousingTextureBehavior(),
            0x5561F005 => new TypeCache.InteractiveMusicBehavior(),
            0x47B73B64 => new TypeCache.ItemFinderBehavior(),
            0x7F13B8CC => new TypeCache.JewelVaultBehavior(),
            0x64DA1F0C => new TypeCache.LeashedPathMovementBehaviorClient(),
            0x618186AD => new TypeCache.LinearSoundBehavior(),
            0x05899C95 => new TypeCache.MobMonsterMagicBehavior(),
            0x3BAC5871 => new TypeCache.MonsterArenaBehavior(),
            0x62263818 => new TypeCache.MountItemBehavior(),
            0x0B6BCC70 => new TypeCache.MountSoundBehavior(),
            0x52EA8495 => new TypeCache.MoveBehaviorClient(),
            0x38907C20 => new TypeCache.ObstacleCourseCatapultBehaviorClient(),
            0x23996AEC => new TypeCache.ObstacleCourseFinishLineBehaviorClient(),
            0x086C665C => new TypeCache.ObstacleCourseModifyTimeBehaviorClient(),
            0x53D59484 => new TypeCache.ObstacleCoursePendulumBehaviorClient(),
            0x0CD27E1A => new TypeCache.ObstacleCoursePusherBehaviorClient(),
            0x52C907F5 => new TypeCache.ObstacleCourseRevolvingDoorBehaviorClient(),
            0x7E2AF152 => new TypeCache.ObstacleCourseSpeedUpBehaviorClient(),
            0x477EE659 => new TypeCache.ObstacleCourseSpringboardBehaviorClient(),
            0x5FCFD3F6 => new TypeCache.PathBehaviorClient(),
            0x5FC1CF76 => new TypeCache.PestBehavior(),
            0x4EFBA675 => new TypeCache.PetDerbyObstacleBehaviorClient(),
            0x074A42D2 => new TypeCache.PhysicsBehaviorClient(),
            0x2D701D90 => new TypeCache.PositionalSoundBehavior(),
            0x2B542F69 => new TypeCache.PositionalStateSoundBehavior(),
            0x660CC909 => new TypeCache.RidableBehavior(),
            0x070A552D => new TypeCache.ScriptBehavior(),
            0x5FC99777 => new TypeCache.SeedBehavior(),
            0x2421EE88 => new TypeCache.StatePositionalSoundBehavior(),
            0x199B6F35 => new TypeCache.TeleportProximityBehavior(),
            0x030B2DBD => new TypeCache.TreasureCardPosterBehavior(),
            0x2C2D02BC => new TypeCache.TreasureCardVaultBehavior(),
            0x372E5D42 => new TypeCache.WhirlyBurlyBehavior(),
            0x1806E68C => new TypeCache.WhirlyBurlyKioskBehavior(),
            0x5FCD9562 => new TypeCache.WizardClientDuelBehavior(),
            _ => null,
        };
    }

    public class PathMovementBehavior : TypeCache.BehaviorInstance {
        public override uint GetHash() => 582069645;

        [Property(769135219, 7)] public Single m_movementSpeed;
        [Property(768663914, 7)] public Single m_movementScale;
    }

    public class BasicMobileBehavior : TypeCache.BehaviorInstance {
        public override uint GetHash() => 1616662572;
    }
}
