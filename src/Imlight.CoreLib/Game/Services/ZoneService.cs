/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * ZONE SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages zone interactions, player transfers, and zone-specific 
 * mechanics within the game server session.
 * 
 * USAGE EXAMPLE:
 * Internal service handling complex zone transition, spawning, 
 * and player management processes.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty, Jeff
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Imcodec.Types;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Services;

internal class ZoneService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {

    private const int ZONE_REMOVAL_WAIT_TIME_IN_SECONDS = 8;
    private const int ZONE_TRANSFER_CLEANUP_WAIT_TIME_IN_SECONDS = 1;
    private const int ZONE_HEAL_TICK_INTERVAL_IN_SECONDS = 5;
    private const float TELEPORT_EFFECTS_TIME = 2.0f;
    private const string ENTER_ZONE_EVENT_NAME = "EnterZone";

    public IActorRef ZoneActor;
    public ITimerScheduler Timers { get; set; }

    private readonly TimeSpan _zoneRemovalWaitTime = TimeSpan.FromSeconds(ZONE_REMOVAL_WAIT_TIME_IN_SECONDS);
    private readonly bool _randomBackflips
        = ConfigurationManager.Settings["April Fools.RandomBackFlips"].AsBool();
    private bool _isTransferQueued;

    private readonly CoreObjectSerializer _effectSerializer = new(
        behaviors: SerializerFlags.None
    );
    private readonly CoreObjectSerializer _zoneObjectSerializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new ZoneService(parentActor));

    protected override void OnPreDispose() {
        var gameObj = GetActiveGameObject();
        if (gameObj is null) {
            return;
        }

        var globalId = gameObj.m_globalID;

        // If the zone reference is not null, we'll tell the zone to remove the player.
        ZoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLAYER() {
            PlayerActor = SessionActor.ActorRef,
            GlobalId = globalId,
            MobileId = gameObj.m_nMobileID,
        });
        ZoneActor = null;

        // Remove the player from the online player collection.
        OnlinePlayerCollection.RemoveOnlinePlayer(SessionActor.SessionID);

        base.OnPreDispose();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        // Send immediate effects.
        var wizard = GetActiveWizard();
        var timeHomeLastClicked = DateTimeOffset.FromUnixTimeSeconds(wizard.TimeHomeLastClicked);
        var timeDifference = DateTimeOffset.UtcNow.Subtract(timeHomeLastClicked);

        if (timeDifference.TotalSeconds < 30) {
            SendCantGoHomeEffect(timeHomeLastClicked);
        }

        var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
            EventName = ENTER_ZONE_EVENT_NAME,
            PlayerActor = SessionActor.ActorRef,
            PlayerGameObject = GetActiveGameObject()
        };
        ZoneActor.Tell(postEventMsg);

        return;
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
        else if (zoneDetails.ErrorCode != 0) {
            // The server has returned an error code. This means the zone transfer failed.
            InformGameClient("Failed to transfer to zone: " + zoneDetails.ErrorMessage, true);
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
        Logger.Debug("Client was not OK with zone transfer!");
        _isTransferQueued = false;
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_PATCHINGBLOCKED))]
    private void ReceivePatchingBlocked(WIZARD_12_PROTOCOL.MSG_PATCHINGBLOCKED message) {
        _isTransferQueued = false;
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

    // This button and the GotoDorm button are locked client-side until level 2.
    // jooty, again? cmon man
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GOHOME))]
    private void ReceiveGoHome(WIZARD_12_PROTOCOL.MSG_GOHOME message) {
        // this teleports the wizard to the world hub, NOT their home/dorm. for that you want MSG_GOTODORM. goofy ahh naming scheme
        var wizard = GetActiveWizard();
        SendTeleportEffects();

        var currentZone = wizard.Zone;
        var zoneMap = WorldHubZones.GetHubForZone(currentZone);
        var tpmsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = zoneMap.m_hubZone,
            DestinationLocation = zoneMap.m_location,
            SendToClient = true,
            OwnerCharId = wizard.CharId,
        };

        wizard.SetTimeHomeLastClicked(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var delay = TimeSpan.FromSeconds(TELEPORT_EFFECTS_TIME);
        Timers.StartSingleTimer("zonetransfer", tpmsg, delay);
    }

    // This button and the GoHome button are locked client-side until level 2.
    // jooty, again? cmon man
    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_GOTODORM))]
    private void ReceiveGotoDorm(WIZARD_12_PROTOCOL.MSG_GOTODORM message) {
        var wizard = GetActiveWizard();
        SendTeleportEffects();

        var tpmsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = "WizardCity/QA_SpawnRate", // just teleporting to gm for now
            DestinationLocation = "Start",
            SendToClient = true,
            OwnerCharId = wizard.CharId,
        };

        wizard.SetTimeHomeLastClicked(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var delay = TimeSpan.FromSeconds(TELEPORT_EFFECTS_TIME);
        Timers.StartSingleTimer("zonetransfer", tpmsg, delay);
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

        var zoneMap = WorldHubZones.GetHubForZone(message.World);
        if (zoneMap is null) {
            Logger.Error("{0} tried to teleport to an invalid world: {1}",
                Logger.Args(GetActiveWizard().CharId, message.World));

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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP))]
    private void ReceiveAddPlayerRsp(ZONE_102_PROTOCOL.MSG_ADDPLAYERRSP message) {
        // I've just been added to a zone. I need to spawn myself for all the other players.
        SpawnMyself();

        if (_randomBackflips) {
            var wizard = GetActiveWizard();
            Timers.StartPeriodicTimer("backflip", new ZONE_102_PROTOCOL.MSG_RANDOMFLIPS {
                ZoneName = wizard.Zone,
                SenderCharID = wizard.CharId
            },
            TimeSpan.FromSeconds(25));
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERADDEDTOZONE))]
    private void ReceiveNewPlayerAddedToZone(ZONE_102_PROTOCOL.MSG_PLAYERADDEDTOZONE message) {
        // A new player has been added to the zone. We need to spawn them.
        // Skip if this is myself.
        if (message.PlayerActor == SessionActor.ActorRef) {
            Logger.Error("{0} {1} received {2} for self.",
                Logger.Args(SessionActor.ActorRef, SessionActor.SessionID, message.GetType()));

            return;
        }

        // Spawn myself for the new player.
        SpawnMyselfFor(message.PlayerActor);
    }

    // Every 25 seconds, a random player in your zone will do a backflip
    // They will only be backflipping for you, nobody else, not even for themselves
    // If you look at the first letter of each variable in this function, it actually spells out the word "gaslit"
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_RANDOMFLIPS))]
    private void ReceiveRandomFlips(ZONE_102_PROTOCOL.MSG_RANDOMFLIPS message) {
        var rand = new Random();
        var players = OnlinePlayerCollection.GetPlayersInZone(message.ZoneName);
        players = players.Where(p => p.CharacterId != message.SenderCharID).ToArray();
        if (players.Length < 1) {
            return;
        }

        players = players.Where(p => p.CharacterId != message.SenderCharID).ToArray();

        var randomPlayerIndex = rand.Next(0, players.Length - 1);
        var randomPlayer = players[randomPlayerIndex];
        var castEffect = new CANTRIPSMESSAGES_57_PROTOCOL.MSG_CASTEFFECT {
            GameObjectID = randomPlayer.CharacterId,
            SpellTemplateID = 1521398842,
            AnimationName = "P_B_Cantrip_Emote_Backflip"
        };

        SendToSocket(castEffect);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERREMOVEDFROMZONE))]
    private void ReceivePlayerRemovedFromZone(ZONE_102_PROTOCOL.MSG_PLAYERREMOVEDFROMZONE message) {
        // A player has been removed from the zone. We need to remove them.
        // Skip if this is myself.
        if (message.PlayerActor == SessionActor.ActorRef) {
            Logger.Error("{0} {1} received {2} for self.",
                Logger.Args(SessionActor.ActorRef, SessionActor.SessionID, message.GetType()));

            return;
        }

        // Remove the player from the zone.
        var removeMsg = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = message.GlobalId
        };

        SendToSocket(removeMsg);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST))]
    private void ReceiveZoneSupervisorBroadcast(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PLAYERMOVE))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_PLAYERMOVE message) {
        // This is an exception. Sometimes the MoveService interval happens as we zone transfer.
        if (ZoneActor is null) {
            return;
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY))]
    private void ReceiveQueryZoneObject(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY message) {
        if (ZoneActor is null) {
            throw new Exception("Zone Reference was null.");
        }

        ZoneActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SENDTOHUB))]
    private void ReceiveBootToHub(ZONE_102_PROTOCOL.MSG_SENDTOHUB message) {
        var wizard = GetActiveWizard();
        var zoneName = wizard.Zone;

        var worldHubMap = WorldHubZones.GetHubForZone(zoneName);
        if (worldHubMap is null) {
            Logger.Error("Could not find world hub mapping for zone {0}",
                Logger.Args(zoneName));

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

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOTELEPORTEFFECTS))]
    private void ReceiveTeleportEffects(CHARACTER_103_PROTOCOL.MSG_DOTELEPORTEFFECTS message) {
        SendTeleportEffects();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEHEALTICK))]
    private void ReceiveZoneHealTick(ZONE_102_PROTOCOL.MSG_ZONEHEALTICK message) {
        var wizard = GetActiveWizard();
        var currentWizardHealth = wizard.GameStats.m_currentHitpoints;
        var maxWizardHealth = wizard.GameStats.m_baseHitpoints;

        // If this wizard is max health, skip.
        if (currentWizardHealth >= maxWizardHealth) {
            return;
        }

        // Update our Wizard server side.
        var healPercent = message.MaxHealthPercent;
        float healAmount = healPercent / 100 * maxWizardHealth;
        var newHealth = Math.Min(currentWizardHealth + (int) healAmount, maxWizardHealth);

        wizard.UpdateHealth(newHealth);

        // Inform the client about the new health changes.
        // The client has a max health increase effect applied, so sending it here would double the health client side.
        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxHealth = baseStats.m_hitpoints;

        var networkMessage = new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH() {
            CharacterID = wizard.GameObject.m_globalID,
            NewHealth = newHealth,
            NewHealthMax = normMaxHealth,
            DisplayDiff = 1,
        };
        SendToSocket(networkMessage);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REALM_INFO_QUERY))]
    private void ReceiveRealmInfoQuery(GAME_5_PROTOCOL.MSG_REALM_INFO_QUERY message) {
        // Query the LoginServer's GameServerPool for the realm list.
        var realmListMsg = new SERVER_100_PROTOCOL.MSG_REALMLIST();
        var realmList = AskServer<SERVER_100_PROTOCOL.MSG_REALMLIST>(realmListMsg);

        var currentRealm = "Imlight";
        var currentZone = GetActiveWizard()?.Zone ?? "";

        // Query our own game server for the current realm name.
        try {
            var serverInfo = AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(
                new SERVER_100_PROTOCOL.MSG_QUERYSERVER());
            currentRealm = serverInfo.RealmName ?? currentRealm;
        }
        catch { }

        // Serialize the realm list as a RealmInfoList PropertyClass blob.
        // The client expects this exact type — we cannot fabricate the format.
        var realmInfoList = new RealmInfoList {
            m_infoList = []
        };
        for (int i = 0; i < realmList.RealmNames.Length; i++) {
            realmInfoList.m_infoList.Add(new RealmInfo {
                m_realmName = realmList.RealmNames[i],
                m_displayName = realmList.RealmNames[i],
                m_realmPopulation = realmList.PlayerCounts[i]
            });
        }

        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(realmInfoList, (PropertyFlags) 31, out var realmInfoBlob)) {
            Logger.Error("Failed to serialize RealmInfoList for MSG_REALM_INFO_QUERY.");

            return;
        }

        // Serialize an empty instance list — the client requires a valid
        // InstanceInfoList PropertyClass blob, not an empty string.
        var instanceInfoList = new InstanceInfoList {
            m_instanceList = new List<InstanceInfo>()
        };
        if (!serializer.Serialize(instanceInfoList, (PropertyFlags) 31, out var instanceInfoBlob)) {
            Logger.Error("Failed to serialize InstanceInfoList for MSG_REALM_INFO_QUERY.");

            return;
        }

        var rsp = new GAME_5_PROTOCOL.MSG_REALM_INFO_QUERY {
            RealmInfoList = realmInfoBlob,
            CurrentRealm = currentRealm,
            InstanceInfoList = instanceInfoBlob,
            CurrentZone = currentZone
        };
        SendToSocket(rsp);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_TRANSFER_REALMS))]
    private void ReceiveTransferRealms(GAME_5_PROTOCOL.MSG_TRANSFER_REALMS message) {
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();

        if (wizard is null || account is null) {
            return;
        }

        // Ask the LoginServer to create a session key on the target realm's game server.
        var createKeyMsg = new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY {
            Account = account,
            TargetRealmName = message.RealmName
        };

        SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP keyRsp;
        try {
            keyRsp = AskServer<SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP>(createKeyMsg);
        }
        catch {
            Logger.Error("Failed to create player key for realm transfer to {Realm}.",
                Logger.Args(message.RealmName));

            return;
        }

        if (!keyRsp.Success) {
            Logger.Warning("Realm transfer to {Realm} failed — realm not found.",
                Logger.Args(message.RealmName));

            return;
        }

        // Send MSG_SERVERTRANSFER to redirect the client to the new game server.
        var serverTransfer = new GAME_5_PROTOCOL.MSG_SERVERTRANSFER {
            IP = keyRsp.IP,
            TCPPort = keyRsp.Port,
            UDPPort = keyRsp.Port,
            Key = 0, // Session key is already stored on the target server.
            UserID = account.AccountId,
            CharID = wizard.CharId,
            ZoneName = wizard.Zone,
            ZoneID = new Imcodec.Types.GID((ulong) keyRsp.Port),
            Location = Util.GetCompactStringFromVector(wizard.Location, wizard.Orientation),
            Slot = 0,
            SessionSlot = 0,
            SessionID = 0,
            TargetPlayerID = wizard.CharId,
            TransitionID = 1,
            FallbackIP = keyRsp.IP,
            FallbackTCPPort = keyRsp.Port,
            FallbackUDPPort = keyRsp.Port,
            FallbackZone = wizard.Zone,
            FallbackZoneID = new Imcodec.Types.GID((ulong) keyRsp.Port)
        };
        SendToSocket(serverTransfer);
    }

    [MessageHandler(typeof(GAME2_55_PROTOCOL.MSG_CURRENTREALM))]
    private void ReceiveCurrentRealm(GAME2_55_PROTOCOL.MSG_CURRENTREALM message) {
        var wizard = GetActiveWizard();
        var currentZone = wizard?.Zone ?? "";

        var currentRealm = "Imlight";
        try {
            var serverInfo = AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(
                new SERVER_100_PROTOCOL.MSG_QUERYSERVER());
            currentRealm = serverInfo.RealmName ?? currentRealm;
        }
        catch { }

        var rsp = new GAME2_55_PROTOCOL.MSG_CURRENTREALM {
            CurrentRealm = currentRealm,
            CurrentZone = currentZone
        };
        SendToSocket(rsp);
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
                PlayerActor = SessionActor.ActorRef,
                GlobalId = GetActiveGameObject().m_globalID,
                IsPlayerStillConnected = true,
                MobileId = GetActiveGameObject().m_nMobileID
            };
            _ = ZoneActor.Ask<ZONE_102_PROTOCOL.MSG_REMOVEPLAYERRSP>(removePlayerMsg, _zoneRemovalWaitTime).Result;

            // Remove the player from the online player collection.
            OnlinePlayerCollection.RemoveOnlinePlayer(SessionActor.SessionID);
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
            LocationX = (ushort) compressedCoords.X,
            LocationY = (ushort) compressedCoords.Y,
            LocationZ = (ushort) compressedCoords.Z,
            Direction = (byte) coords.W,
            MobileID = GetActiveGameObject().m_nMobileID,
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = serverTele,
            Selfless = false,
        };
        ReceiveZoneBroadcast(broadcastMsg);
    }

    private void SpawnMyself() {
        var wizard = GetActiveWizard();
        var properGameObj = WizardObjectLoader.GetPlayerGameObject(wizard);

        var flags = PropertyFlags.Prop_Public | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!_zoneObjectSerializer.Serialize(properGameObj, flags, out var gameObjData)) {
            Logger.Error("Failed to serialize game object for {0}",
                Logger.Args(wizard.CharId));

            return;
        }

        var addMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = gameObjData,
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = addMsg
        };

        ZoneActor.Tell(broadcastMsg);
    }

    private void SpawnMyselfFor(IActorRef actorRef) {
        var wizard = GetActiveWizard();
        var properGameObj = WizardObjectLoader.GetPlayerGameObject(wizard);

        var flags = PropertyFlags.Prop_Public | PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!_zoneObjectSerializer.Serialize(properGameObj, flags, out var gameObjData)) {
            Logger.Error("Failed to serialize game object for {0}",
                Logger.Args(wizard.CharId));

            return;
        }

        var addMsg = new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = gameObjData,
        };

        actorRef.Tell(addMsg);
    }

    private void SendTeleportEffects() {
        var wizard = GetActiveWizard();
        var now = DateTimeOffset.UtcNow;

        SendCantGoHomeEffect(now);

        // what does this do? who knows! its probably important.
        var enterState = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = wizard.GameObject.m_globalID,
            State = StringHash.Compute("Teleport"),
        };
        var broadcastWrapper = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = enterState,
            Selfless = false,
        };
        ReceiveZoneBroadcast(broadcastWrapper);

        SendRecallHomeEffect(now);
    }

    private void SendCantGoHomeEffect(DateTimeOffset unixTimeStart) {
        var wizard = GetActiveWizard();
        NamedEffect effect = new NamedEffect {
            m_effectNameID = StringHash.Compute("CantGoHome"),
            m_endTime = (uint) unixTimeStart.AddSeconds(30).ToUnixTimeSeconds(),
            m_internalID = wizard.GameEffects.Count,
        };

        wizard.GameEffects.Add(effect);

        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!_zoneObjectSerializer.Serialize(effect, flags, out var serializedEffect)) {
            Logger.Error("Failed to serialize game object for {0}",
                Logger.Args(wizard.CharId));

            return;
        }

        var addEffect = new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectData = serializedEffect
        };

        SendToSocket(addEffect);
    }

    private void SendRecallHomeEffect(DateTimeOffset time) {
        var wizard = GetActiveWizard();

        // on live servers, the end time is 200 seconds from the time gohome is sent. i still have no clue why.
        // also on live servers, when teleporting in zone, it will send the effects like 3 times. i also have no clue on this either.
        var effect = new NamedEffect {
            m_effectNameID = StringHash.Compute("RecallHome"),
            m_endTime = (uint) time.AddSeconds(2).ToUnixTimeSeconds(),
            m_internalID = wizard.GameEffects.Count,
        };

        wizard.GameEffects.Add(effect);

        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        if (!_effectSerializer.Serialize(effect, flags, out var serializedEffect)) {
            Logger.Error("Failed to serialize game object for {0}",
                Logger.Args(wizard.CharId));

            return;
        }

        var addEffect = new GAME_5_PROTOCOL.MSG_ADDEFFECT {
            GameObjectID = wizard.GameObject.m_globalID,
            EffectData = serializedEffect
        };
        var broadcastWrapper = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = addEffect,
            Selfless = false,
        };
        ReceiveZoneBroadcast(broadcastWrapper);
    }

}
