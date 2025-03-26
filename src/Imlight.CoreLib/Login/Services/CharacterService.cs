/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.Login.Services;

internal class CharacterService(SessionActor parentActor) : MessageService(parentActor) {
    
    private uint _characterCreationStage;
    private uint _characterCreationParameter;

    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new CharacterService(parentActor));

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER))]
    private void ReceiveCreateCharacter(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER message) {
        var account = GetSocketAccount();
        if (account is null) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE { ErrorCode = 1 });
            
            return;
        }

        // The client has sent us serialized WizardCharacterCreationData. We need to
        // deserialize it to add it to our account database.
        var serializer = new ObjectSerializer(
            Behaviors: SerializerFlags.None,
            Versionable: false
        );

        // Deserializing the creation data may sometimes fail if the client is using a different version of the game.
        // Instead of totally failing, we'll catch the exception and send an error message to the client.
        try {
            var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
            if (!serializer.Deserialize(message.CreationInfo, flags, out WizardCharacterCreationInfo charData)) {
                throw new Exception("Failed to deserialize character creation data.");
            }

            var newCharacter = CharacterHelper.CreateCharacterFromCreationInfo(charData);
            var createdCharacter = account.AddCharacter(newCharacter);

            // Craft log arguments.
            var wizardName = newCharacter.PlayerNameBehavior.GetWizardName();
            var school = (MagicSchool) charData.m_schoolOfFocus;
            var gender = charData.m_avatarBehavior.m_eGender;
            var logs = Logger.Args(account.Username, wizardName, charData);

            if (createdCharacter) {
                Logger.Information("Account {accountUsername} created new character {wizardName}", logs);

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE { ErrorCode = 0 });
            }
            else {
                Logger.Error("Account {accountUsername} failed to add character to database.", logs);

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE { ErrorCode = 1 });
            }
        }
        catch (Exception e) {
            Logger.Error("Account {accountUsername} failed to deserialize character creation data. {Exception}", 
                Logger.Args(account.Username, e.Message));

            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE { ErrorCode = 1 });

            throw;
        }
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER))]
    private void ReceiveDeleteCharacter(LOGIN_7_PROTOCOL.MSG_DELETECHARACTER message) {
        var errorCode = 0;
        var account = GetSocketAccount();
        if (account is null) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_DELETECHARACTERRESPONSE { ErrorCode = 1 });

            return;
        }

        var deletedCharacter = account.DeleteCharacter(message.CharID);

        // If we had no problems deleting the character from the account, delete the character from the database.
        if (deletedCharacter) {
            var deletedCharacterFromCollection = WizardCollection
                .DeleteCharacter(message.CharID);
            var deletedCharacterFromAccount = AccountCollection
                .DeleteCharacterFromAccount(account.AccountId, message.CharID);

            if (!deletedCharacterFromCollection || !deletedCharacterFromAccount) {
                Logger.Error("Account {accountUsername} failed to delete character {characterId} from database.",
                    Logger.Args(account.Username, message.CharID));
                errorCode = 1;
            }
        }
        else {
            Logger.Error("Account {accountUsername} failed to delete character {characterId} from account.",
                Logger.Args(account.Username, message.CharID));
            errorCode = 1;
        }

        if (errorCode == 0) {
            Logger.Information("Account {accountUsername} deleted character {characterId}.",
                Logger.Args(account.Username, message.CharID));
        }

        SendToSocket(new LOGIN_7_PROTOCOL.MSG_DELETECHARACTERRESPONSE { ErrorCode = errorCode });
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST))]
    private void ReceiveRequestCharacterList(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST message) {
        var account = GetSocketAccount();
        if (account is null) {
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST() { Error = 1 });

            return;
        }

        // Tell the client we're going to start sending the character list.
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());

        // For every character, we're going to serialize the document and send to the client.
        if (account.Characters.Count > 0) {
            var serializer = new ObjectSerializer(
                Behaviors: SerializerFlags.None,
                Versionable: false
            );

            for (int i = 0; i < account.Characters.Count; i++) {
                var character = account.Characters[i];

                // Database is document-based. We need to serialize the document to send to the client.
                var loginScreenInfo = CharacterHelper.GetLoginScreenInfo(character);

                // Serialize the character info to send to the client.
                var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
                if (!serializer.Serialize(loginScreenInfo, flags, out var data)) {
                    Logger.Error("Account {accountUsername} failed to serialize character {characterId} for login screen.",
                        Logger.Args(account.Username, character.CharId));

                    return;
                }

                SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO() { CharacterInfo = data });
            }
        }

        // Tell the client we've finished sending the character list.
        SendToSocket(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION))]
    private void ReceiveLoginLogCharacterCreation(LOGIN_7_PROTOCOL.MSG_LOGINLOGCHARACTERCREATION message) {
        this._characterCreationParameter = message.Parameter;
        this._characterCreationStage = message.Stage;
    }

}
