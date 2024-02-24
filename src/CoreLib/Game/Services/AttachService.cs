/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Net;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class AttachService : MessageService {
    public AttachService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new AttachService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ATTACH))]
    private void ReceiveAttach(GAME_5_PROTOCOL.MSG_ATTACH message) {
        // Use the session key given in the message to ensure that the user didn't bypass our login server.
        // The key will be associated with the account they're trying to log into.
        if (!ValidateLoginKey(message.LoginKey, message.UserID, out var account)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED() {
                Error = 1,
                Rejected = 1,
            });
            throw new SessionFatalException(
                $"User [{message.UserID}] failed to validate login key: {message.LoginKey}.");
        }
        if (!GetWizardFromAccount(account, message.CharID, out var wizard)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED() {
                Error = 1,
                NoDisconnect = 1, // @todo: find out what these error codes mean.
                Rejected = 1,
            });
            throw new SessionFatalException($"User [{message.UserID}] tried to attach with a character " +
                                            $"they did not have.");
        }

        // This is the first authentication action the user will send on the game server. Send messages to the
        // other services denoting both the account and character this SessionActor just logged into.
        SetAccountInternally(account);
        SetCharacterInternally(wizard);

        // Tell the game server that the user has attached, and now we need to find a zone process for their
        // zone, or create a new one. This is an internal zone transfer that does not involve the client.
        var zoneDetails = InternalZoneTransfer(message.ZoneName);
        if (zoneDetails.ErrorCode != 0) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_ATTACHFAILED { Error = zoneDetails.ErrorCode });
            return;
        }

        // Set the character's location and zone to the ones given in the message.
        wizard.SetZone(message.ZoneName, zoneDetails.ZoneDisplayName);

        // The location is a string marked with commas. Parse it into a Vector3.
        // We're compressing the orientation by a factor of 0.708 to fit it into a byte.
        // This is to remain consistent with the client's representation of orientation.
        var location = Util.GetVectorFromCompactString(message.Location);
        var actualLocation = new Vector3(location.X, location.Y, location.Z);
        var orientation = location.W / CharacterHelper.OrientationCompressionFactor;
        wizard.SetPersistentLocation(actualLocation);
        wizard.SetPersistentOrientation((byte) orientation);

        // Tiny anti-cheat measure. When the character object is created, we recalculate the game stats.
        CharacterHelper.RecalculateGameStats(wizard);

        // Get the best game server for this user.
        var gameServer = GetGameServer();
        wizard.GameServerIp = gameServer.IP;
        wizard.GameServerPort = (ushort) gameServer.Port;

        // Craft the GameObject for this Wizard.
        var charGameObject = WizardObjectLoader.GetPlayerGameObject(wizard);

        // Set the mobile id to the one given by the zone.
        charGameObject.m_nMobileID = zoneDetails.MobileId;

        // Set the Wizard's GameObject reference to what we just created.
        wizard.GameObject = charGameObject;

        // Serialize the GameObject and send it to the client.
        var localGameObjectData = new CoreObjectSerializer().Serialize(charGameObject);
        if (charGameObject is null || string.IsNullOrEmpty(localGameObjectData)) {
            throw new ServiceRetryException($"User {message.UserID} failed to grab or deserialize " +
                                            $"their player object.");
        }

        var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE() {
            RealmName = "Centaur",

            // Set character data.
            Data = localGameObjectData,
            IsCSR = account.AuthLevel > AuthLevel.QualityAssurance ? 1 : 0,
            Permissions = 31679, // @todo: these permissions look like bitflags. Find out what they mean.

            // Set zone data.
            ZoneName = message.ZoneName,
            ZoneID = message.ZoneID,
            DynamicZoneID = zoneDetails.DynamicZoneId,
            DynamicServerProcID = zoneDetails.DynamicZoneId,

            // Misc
            ShowSubscriberIcon = 0,
            TestServer = 0
        };

        var actualWizardName = WizardNameBank.GetEnglishName(wizard.PlayerNameBehavior.NameIndices, wizard.WizardAvatar.m_eGender);
        AddPlayerToZone(charGameObject, wizard);

        SendToSocket(loginCompleteMsg);

        // Inform the other services that attach is complete.
        TellOtherServices(new SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE());
    }

    private ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP InternalZoneTransfer(string zoneName) {
        var zoneMsg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = zoneName,
            SendToClient = false
        };
        return AskOtherService<ZONE_102_PROTOCOL.MSG_ZONETRANSFERRSP>(zoneMsg);
    }

    private void AddPlayerToZone(WizClientObject charObj, Wizard wizard) {
        var msg = new ZONE_102_PROTOCOL.MSG_ADDPLAYER {
            Player = SessionActor.ActorRef,
            PlayerObject = charObj,
            Wizard = wizard,
            ActualWizardName = wizard.PlayerNameBehavior.GetWizardName(),
        };
        TellOtherServices(msg);
    }

    private bool GetWizardFromAccount(Account account, ulong charId, out Wizard character) {
        var result = account.GetCharacter(charId);
        character = result;

        return result is not null;
    }

    private SERVER_100_PROTOCOL.MSG_SERVERINFO GetGameServer() {
        var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();

        return AskServer<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg);
    }

    private bool ValidateLoginKey(ByteString key, ulong userId, out Account account) {
        account = null;

        var msg = new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY() {
            Key = key,
            UserID = userId,
            SessionActor = SessionActor
        };
        var rsp = AskServer<SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP>(msg);

        account = rsp.Account;
        return rsp.ErrorCode == 0;
    }

    private void SetAccountInternally(Account account) {
        // Tell the SessionActor to set the account.
        TellOtherServices(new ACCOUNT_104_PROTOCOL.MSG_ACCOUNT() {
            Account = account
        });
    }

    private void SetCharacterInternally(Wizard character) {
        TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_SETACTIVEWIZARD {
            Wizard = character
        });
    }
}
