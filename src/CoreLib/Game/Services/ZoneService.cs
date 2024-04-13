/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.World;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

public class ZoneService : MessageService {
    private const int ZONE_REMOVAL_WAIT_TIME_IN_SECONDS = 4;
    private const int ZONE_TRANSFER_CLEANUP_WAIT_TIME_IN_SECONDS = 1;

    public IActorRef ZoneActor;

    private readonly TimeSpan _zoneRemovalWaitTime = TimeSpan.FromSeconds(ZONE_REMOVAL_WAIT_TIME_IN_SECONDS);
    private bool _isTransferQueued;

    private readonly CoreObjectSerializer _effectSerializer = new CoreObjectSerializer()
                    .OnBehaviors(SerializerOptions.Behaviors.None)
                    .OnPropertyMask(SerializerOptions.PropertyFlags.Transmit
                                  | SerializerOptions.PropertyFlags.AuthorityTransmit);

    public ZoneService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) => Akka.Actor.Props.Create(() => new ZoneService(parentActor));

    protected override void OnPreDispose() {
        var gameObj = GetActiveGameObject();
        if (gameObj is null) {
            return;
        }

        var globalId = gameObj.m_globalID;

        // If the zone reference is not null, we'll tell the zone to remove the player.
        ZoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            Player = SessionActor.ActorRef,
            GlobalId = globalId
        });
        ZoneActor = null;

        base.OnPreDispose();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        if (_isTransferQueued) {
            return;
        }

        // Sending the server transfer request to the server will allocate and load the zone.
        var zoneDetails = AskServer<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(message);
        if (message.SendToClient && zoneDetails.ErrorCode == 0) {
            // Check if the destination zone is the same as the current zone. If so, just teleport the player.
            if (message.DestinationZone == GetActiveWizard().Zone) {
                DoTeleport(message.DestinationLocation);
                return;
            }

            ReadyClientForZoneTransfer(message);
        }
        else {
            // If we're not sending this message to the client, it means the zone is being loaded
            // for MSG_ATTACH. In which case, the client is already prepared for the zone transfer.
            SetZone(zoneDetails.ZoneActorRef);
        }

        Sender.Tell(zoneDetails);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK))]
    private void ReceiveZoneTransferAck(GAME_5_PROTOCOL.MSG_ZONETRANSFERACK message) {
        // The client has accepted the zone transfer. We can now send the server transfer message.
        DoZoneTransfer();
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK))]
    private void ReceiveZoneTransferNack(GAME_5_PROTOCOL.MSG_ZONETRANSFERNACK message) {
        // The client has denied the zone transfer.
        Logger.Debug("Client was not OK with zone transfer! Possibly patching.");
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_RETRYTELEPORT))]
    private void ReceiveRetryTeleport(GAME_5_PROTOCOL.MSG_RETRYTELEPORT message) {
        DoZoneTransfer();
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_ZONEHOP))]
    private void ReceiveZoneHop(WIZARD2_53_PROTOCOL.MSG_ZONEHOP message) {
        // This message is sent when the client has enabled classic mode and wants to reload their current zone.
        var character = GetActiveWizard();

        _isTransferQueued = true;
        var zoneTransferRequestMessage = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST {
            ZoneName = character.Zone,
            SendAck = 0
        };
        SendToSocket(zoneTransferRequestMessage);

        character.QueuedZoneName = character.Zone;
        character.QueuedZoneLocation = Util.GetCompactStringFromVector(character.Location, character.Orientation);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GOHOME))]
    private void ReceiveGoHome(WIZARD_12_PROTOCOL.MSG_GOHOME message) {
        // this teleports the wizard to the world hub, NOT their home/dorm. for that you want MSG_GOTODORM. goofy ahh naming scheme
        var wizard = GetActiveWizard();
        SendButtonTeleportEffects();

        var currentZone = wizard.Zone;
        var zoneMap = WorldHubZones.GetHubZoneMapping(currentZone);
        var tpmsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = zoneMap.m_hubZone,
            DestinationLocation = zoneMap.m_location,
            SendToClient = true
        };
        
        wizard.SetTimeHomeLastClicked(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Task.Run(async () => await Task.Delay(TimeSpan.FromSeconds(2))).Wait();
        ReceiveZoneTransferRequest(tpmsg);
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceiveAttachComplete(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();
        var timeHomeLastClicked = DateTimeOffset.FromUnixTimeSeconds(wizard.TimeHomeLastClicked);
        var timeDifference = DateTimeOffset.UtcNow.Subtract(timeHomeLastClicked);
        
        if (timeDifference.TotalSeconds < 30) {
            SendCantGoHomeEffect(timeHomeLastClicked);
        }
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTREQUEST))]
    private void ReceiveWorldTeleportRequest(WIZARD_12_PROTOCOL.MSG_WORLDTELEPORTREQUEST message) {
        if (message.World.Length == 0) { // user clicked "exit", remove the wizbang
            var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG() {
                GameObjectID = GetActiveWizard().CharId,
                WizBangID = (uint) WizBangs.None
            };

            ZoneBroadcast(wizBangMsg, false);
            return;
        }

        var zoneMap = WorldHubZones.GetHubZoneMapping(message.World);
        if (zoneMap is null) {
            Logger.Error("{0} tried to teleport to an invalid world: {1}", Logger.Args(GetActiveWizard().CharId, message.World));
            return;
        }

        var zoneName = zoneMap.m_universeTPZone;
        var zoneLocation = zoneMap.m_universeTPLocation;

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = zoneName,
            DestinationLocation = zoneLocation,
            SendToClient = true
        };
        ReceiveZoneTransferRequest(msg);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    private void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message) {
        // This is an internal message from MSG_ATTACH to add the player to the zone.
        if (ZoneActor is null) {
            throw new NullReferenceException(nameof(ZoneActor));
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_FISHINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_FISHINTERACTION message) {
        // This is an exception. Sometimes the MoveService interval happens as we zone transfer.
        if (ZoneActor is null) {
            return;
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    private void ReceiveQueryZoneObject(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SENDTOHUB))]
    private void ReceiveBootToHub(ZONE_102_PROTOCOL.MSG_SENDTOHUB message) {
        var wizard = GetActiveWizard();
        var zoneName = wizard.Zone;

        var worldHubMap = WorldHubZones.GetHubZoneMapping(zoneName);
        if (worldHubMap is null) {
            Logger.Error("Could not find world hub mapping for zone {0}", Logger.Args(zoneName));
            return;
        }

        var destinationZoneName = worldHubMap.m_hubZone;
        var destinationZoneLocation = worldHubMap.m_location;

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = destinationZoneName,
            DestinationLocation = destinationZoneLocation,
            SendToClient = true
        };

        ReceiveZoneTransferRequest(msg);
    }

    private void SetZone(IActorRef actorRef) {
        ZoneActor = actorRef;
    }

    private void ReadyClientForZoneTransfer(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        var character = GetActiveWizard();
        _isTransferQueued = true;

        // Ask the client if it's okay with being transferred.
        var msg = new GAME_5_PROTOCOL.MSG_ZONETRANSFERREQUEST {
            ZoneName = message.DestinationZone,
            SendAck = 1
        };
        SendToSocket(msg);

        character.QueuedZoneName = message.DestinationZone;
        character.QueuedZoneLocation = message.DestinationLocation;
    }

    private void DoZoneTransfer() {
        var account = GetSocketAccount();
        var character = GetActiveWizard();

        // Remove the player from their current zone. We're awaiting a reply so the zone can properly clean up
        // before we continue on potentially a different thread.
        try {
            var removePlayerMsg = new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
                Player = SessionActor.ActorRef,
                GlobalId = GetActiveGameObject().m_globalID,
                IsPlayerStillConnected = true
            };
            _ = ZoneActor.Ask(removePlayerMsg, _zoneRemovalWaitTime).Result;
        }
        catch {
            Logger.Warning("Zone removal timeout of {0} seconds exceeded.", Logger.Args(ZONE_REMOVAL_WAIT_TIME_IN_SECONDS));
        }
        finally {
            // The zone has removed the player, but the client may not have had time to clean up
            // all the zone items. We'll wait a short time here before we send zone transfer.
            var delay = TimeSpan.FromSeconds(ZONE_TRANSFER_CLEANUP_WAIT_TIME_IN_SECONDS);
            Task.Run(async () => await Task.Delay(delay)).Wait();

            // When we send this message, the client will disconnect from the current zone and reconnect to the next.
            // This means attach will happen again, so this is all we need to do here.
            var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER() {
                IP = character.GameServerIp,
                TCPPort = character.GameServerPort,
                UDPPort = character.GameServerPort,
                UserID = account.AccountId,
                CharID = character.CharId,
                ZoneName = character.QueuedZoneName,
                Location = character.QueuedZoneLocation,
                Slot = 0,
                SessionSlot = 0,
                SessionID = 0,
                TargetPlayerID = character.CharId,
                TransitionID = 1,
                FallbackIP = character.GameServerIp,
                FallbackTCPPort = character.GameServerPort,
                FallbackUDPPort = character.GameServerPort,
                FallbackZone = character.Zone
            };
            SendToSocket(serverTransfer);
        }
    }

    private void DoTeleport(string location) {
        var coords = Util.GetVectorFromCompactString(location);
        var compressedCoords = coords / 4;

        var serverTele = new GAME_5_PROTOCOL.MSG_SERVERTELEPORT() {
            LocationX = (ushort)compressedCoords.X,
            LocationY = (ushort)compressedCoords.Y,
            LocationZ = (ushort)compressedCoords.Z,
            Direction = (byte) coords.W,
            MobileID = GetActiveGameObject().m_nMobileID,
        };
        SendToSocket(serverTele);
    }

    private void SendButtonTeleportEffects() {
        var wizard = GetActiveWizard();
        var now = DateTimeOffset.UtcNow;
        
        SendCantGoHomeEffect(now);

        var enterState = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = wizard.GameObject.m_globalID,
            State = 455326240,
            Data = "",
            IgnoreIfCurrentStateIsOff = 0
        };

        SendToSocket(enterState);

        SendRecallHomeEffect(now);
    }

    private void SendCantGoHomeEffect(DateTimeOffset unixTimeStart) {
        var wizard = GetActiveWizard();
        NamedEffect effect = new NamedEffect {
            m_bIsOnPet = false,
            m_currentTickCount = 0,
            m_effectNameID = StringHash.Compute("CantGoHome"),
            m_endTime = (uint) unixTimeStart.AddSeconds(30).ToUnixTimeSeconds(),
            m_internalID = wizard.GameEffects.Count,
            m_itemSlotID = 0,
            m_originatorID = new GID { Value = 0 },
            m_overrideName = ""
        };
        var serializedEffect = _effectSerializer.Serialize(effect);
        wizard.GameEffects.Add(effect);

        var addEffect = new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectData = serializedEffect
        };
        
        SendToSocket(addEffect);
    }

    private void SendRecallHomeEffect(DateTimeOffset time) {
        var wizard = GetActiveWizard();
        NamedEffect effect = new NamedEffect {
            m_bIsOnPet = false,
            m_currentTickCount = 0,
            m_effectNameID = StringHash.Compute("RecallHome"),
            m_endTime = (uint) time.AddSeconds(2).ToUnixTimeSeconds(),
            m_internalID = wizard.GameEffects.Count,
            m_itemSlotID = 0,
            m_originatorID = new GID { Value = 0 },
            m_overrideName = ""
        };
        
        wizard.GameEffects.Add(effect);
        var serializedEffect = _effectSerializer.Serialize(effect);
        var addEffect = new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectData = serializedEffect
        };
        SendToSocket(addEffect);
    }
}
