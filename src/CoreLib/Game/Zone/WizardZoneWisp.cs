/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

public class WizardZoneWisp : WizardZoneCreature {
    private enum WispType {
        Health = 88010,
        Mana = 88011,
        Gold = 88012
    }
    private readonly WispType _wispType;

    // ctor
    public WizardZoneWisp(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef)
            : base(activeGameObject, template, path, startingNodeIndex, wizardZoneRef) {
        // Decide what type of wisp this is.
        var gObjTemplate = template as WizGameObjectTemplate;
        var gObjTemplateName = gObjTemplate.m_objectName.ToString();

        if (gObjTemplateName.Contains("Health")) {
            _wispType = WispType.Health;
        } else if (gObjTemplateName.Contains("Mana")) {
            _wispType = WispType.Mana;
        } else if (gObjTemplateName.Contains("Gold")) {
            _wispType = WispType.Gold;
        } else {
            Logger.Error("Unknown wisp {objName} with template ID {templateId}", Logger.Args(gObjTemplateName, gObjTemplate.m_templateID));
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject,
                              CoreTemplate template,
                              WizardZonePath path,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef) => Akka.Actor.Props.Create(()
            => new WizardZoneWisp(activeGameObject, template, path, startingNodeIndex, wizardZoneRef));

    protected override void OnPlayerProximityEnter(CoreObject suspectObject, IActorRef suspectActor) {
        var serializer = new ObjectSerializer()
          .OnBehaviors(SerializerOptions.Behaviors.None)
          .OnPropertyMask((SerializerOptions.PropertyFlags) 1);

        // Query and retrieve wizard that interacted with wisp.
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = suspectActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        // Values with gear and effects.
        var baseHealth = wizard.GameStats.m_baseHitpoints;
        var currentHealth = wizard.GameStats.m_currentHitpoints;
        var baseMana = wizard.GameStats.m_baseMana;
        var currentMana = wizard.GameStats.m_currentMana;

        // Values before effects are applied.
        var clientGameStats = wizard.GameStats.GetClientTypeAlternative();
        var msgBaseHealth = clientGameStats.m_baseHitpoints;
        var msgBaseMana = clientGameStats.m_baseMana;

        // Todo: should split each of these into their own
        var name = ((WizGameObjectTemplate)Template).m_objectName.ToString();
        switch (_wispType) {
            // 'WC' HP wisps only appear in Unicorn Way, and heal 40% instead of 25%. 'KT' HP wisps are used everywhere else.
            // 'UW' Mana wips only appear in Unicorn Way, and replenish 25% instead of 10%.
            case WispType.Health:
                if (baseHealth == currentHealth) { // If health is full, no need to heal.
                    return;
                }

                var healthPercentage = name.Contains("KT") ? 0.25f : 0.40f; // Check for wisp type.

                var healthUpdate = (int) (baseHealth * healthPercentage);
                if (currentHealth + healthUpdate > baseHealth) { // Check for overheal.
                    healthUpdate = baseHealth - currentHealth;
                }

                var healthUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
                    CharacterID = wizard.CharId,
                    NewHealth = currentHealth + healthUpdate,
                    NewHealthMax = msgBaseHealth,
                    DisplayDiff = 1
                };
                suspectActor.Tell(healthUpdateMsg);

                // Todo: Get this info from somewhere else. Also, currently only works on first pickup.
                var stateHealth = new EmoteStateOverrideInfo {
                    m_loop = false,
                    m_particleAsset = "Character/FX_WispRed_Dsppr.nif",
                    m_soundAsset = "Sound/GUI/ui_health_powerup_01.wav",
                    m_stateNameID = 1896147676
                };
                var stateHealthData = serializer.Serialize(stateHealth);

                var stateHealthMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                    GameObjectID = wizard.CharId,
                    State = 1896147676,
                    Data = stateHealthData,
                    IgnoreIfCurrentStateIsOff = 0
                };
                suspectActor.Tell(stateHealthMsg);

                wizard.UpdateHealth(currentHealth + healthUpdate);

                break;
            case WispType.Mana:
                if (baseMana == currentMana) { // If mana is full, no need to replenish.
                    return;
                }

                var manaPercentage = name.Contains("UW") ? 0.25f : 0.10f; // Check for wisp type.

                var manaUpdate = (int) (baseMana * manaPercentage); // Check for overflow.
                if (currentMana + manaUpdate > baseMana) {
                    manaUpdate = baseMana - currentMana;
                }

                var manaUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                    Mana = currentMana + manaUpdate,
                    MaxMana = msgBaseMana,
                    DisplayDiff = 1
                };
                suspectActor.Tell(manaUpdateMsg);

                var stateMana = new EmoteStateOverrideInfo {
                    m_loop = false,
                    m_particleAsset = "Character/FX_WispBlue_Dsppr.nif",
                    m_soundAsset = "Sound/GUI/ui_health_powerup_01.wav",
                    m_stateNameID = 1896147676
                };
                var stateManaData = serializer.Serialize(stateMana);

                var stateManaMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
                    GameObjectID = wizard.CharId,
                    State = 1896147676,
                    Data = stateManaData,
                    IgnoreIfCurrentStateIsOff = 0
                };
                suspectActor.Tell(stateManaMsg);

                wizard.UpdateMana(currentMana + manaUpdate);

                break;
            case WispType.Gold:
                // Todo: Randomly generate gold values. Scale with world maybe?
                var goldUpdateMsg = new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
                    Gold = wizard.GameStats.m_currentGold + 100,
                    MaxGold = wizard.GameStats.m_baseGoldPouch
                };
                suspectActor.Tell(goldUpdateMsg);

                wizard.AddGold(100);

                break;
            default:
                break;
        }

        Die(); // Destroy wisp.
    }
}
