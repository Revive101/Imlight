/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using System;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Services;

internal sealed class TutorialService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {

    private const uint TUTORIAL_NAME_STRING_ID = 600062081;
    private const string TUTORIAL_EXTERIOR_ZONE_NAME = "WizardCity/Tutorial_Exterior";

    public ITimerScheduler Timers { get; set; }

    internal static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TutorialService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ATTACH))]
    private void ReceivePostAttach(GAME_5_PROTOCOL.MSG_ATTACH msg) {
        if (msg.ZoneName != TUTORIAL_EXTERIOR_ZONE_NAME) {
            return;
        }

        var tutorialInfo = new TutorialInfo {
            m_tutorialNameID = TUTORIAL_NAME_STRING_ID,
            m_tutorialStage = 0,
        };

        var ser = new ObjectSerializer(Behaviors: SerializerFlags.None, Versionable: false);
        ser.Serialize(tutorialInfo, PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit, out var tutorialInfoBuffer);
        var tutorialMsg = new GAME_5_PROTOCOL.MSG_TUTORIALS() {
            GlobalID = 1,
            Remove = 0,
            TutorialInfo = tutorialInfoBuffer
        };
        SendToSocket(tutorialMsg);
        SendToSocket(tutorialMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_SERVERTUTORIALCOMMAND))]
    private void ReceiveServerTutorialCommand(GAME_5_PROTOCOL.MSG_SERVERTUTORIALCOMMAND msg) {
        switch (msg.GoalToComplete) {
            case "Trigger Storm":
                ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
                    StateName = "Raining",
                    ObjectName = "TutorialStorm",
                    ExclusiveToSender = true,
                    Sender = SessionActor.ActorRef
                });
                SendToSocket(
                    new GAME_5_PROTOCOL.MSG_PLAYSOUND {
                        SoundID = 89062101935790
                    }
                );
                break;
            case "Despawn Ambrose Outside": // not quite sure why it does this, but he probably gets replaced with a walking ambrose
                ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_REMOVEOBJECT {
                    ObjectName = "WC-TUT-NPC01"
                });
                break;
            case "Trigger Rubble":
                ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
                    StateName = "Rubble",
                    ObjectName = "WC-Rubble",
                    ExclusiveToSender = true,
                    Sender = SessionActor.ActorRef
                });
                break;
            case "Trigger Silhouette":
                ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
                    StateName = "walk", // why are you lower case? why cant you just be consistent kingsisle?
                    ObjectName = "MalSilhouetteObj",
                    ExclusiveToSender = true,
                    Sender = SessionActor.ActorRef
                });
                break;
            case "Walk Ambrose":
                ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                    EventName = "WalkAmbrose",
                    PlayerActor = SessionActor.ActorRef
                });
                Timers.StartSingleTimer("DespawnA",
                new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                    EventName = "DespawnA",
                    PlayerActor = SessionActor.ActorRef
                },
                TimeSpan.FromSeconds(5));
                break;
            default:
                break;
        }
    }
}