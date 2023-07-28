/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using Akka.Actor;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Cache;
using WizUnraveler.IO;

namespace Imlight.Server.Game.Services
{
    public class ChatService : MessageService
    {
        private const string FemaleSourcePrefix = "80";
        private const string MaleSourcePrefix = "82";

        public ChatService(SessionActor sessionActor) : base(sessionActor)
        {
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ChatService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
        private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message)
        {
            var globalId = GetActiveCoreObject().m_globalID;
            var character = GetActiveCharacter();
            var nameIndices = character.CreationData.m_nameIndices;
            var gender = character.CreationData.m_avatarBehavior.m_eGender;
            var src = CraftSourceNameFromIndices(nameIndices, gender);

            var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT()
            {
                Message = message.Message,
                SourceID = globalId,
                SourceName = src,
                Filter = 0
            };
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = msg,
                Selfless = true
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
        private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message)
        {
            var globalId = GetActiveCoreObject().m_globalID;
            var character = GetActiveCharacter();
            var nameIndices = character.CreationData.m_nameIndices;
            var gender = character.CreationData.m_avatarBehavior.m_eGender;
            var src = CraftSourceNameFromIndices(nameIndices, gender);

            var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT()
            {
                MessageID = message.MessageID,
                SourceID = globalId,
                SourceName = src,
                Filter = 0,
            };
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = msg,
                Selfless = true
            });
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQASKSERVER))]
        private void ReceiveRequest(GAME_5_PROTOCOL.MSG_REQASKSERVER message)
        {
            
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CORE_EMOTE))]
        private void ReceiveCoreEmote(GAME_5_PROTOCOL.MSG_CORE_EMOTE message)
        {
            SendToSessionServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST()
            {
                Sender = SessionActor.ActorRef,
                Message = message,
                Selfless = true,
            });
        }
        
        private ByteString GetMessagePayload(byte[] input)
        {
            // The message is a wide string.
            var msgBuffer = new BitIterator(input);
            var msgSize = msgBuffer.ReadUInt16() * 2; // Account for unicode
            var msgTextRaw = msgBuffer.ReadBytes(msgSize);
            var msgText = Encoding.Unicode.GetString(msgTextRaw);

            return msgText;
        }

        private ByteString CraftMessagePayload(string input)
        {
            // Convert input string to byte array using Unicode encoding
            var newTextBytes = Encoding.Unicode.GetBytes(input);
            var rebuffer = new BitIterator();

            // Calculate length of byte array, rounded up to nearest multiple of 2
            var len = (ushort)((newTextBytes.Length + 2) / 2);
            rebuffer.WriteUInt16(len);
            rebuffer.WriteBytes(newTextBytes);
            rebuffer.WriteUInt16(32);

            return new ByteString(rebuffer.GetData());
        }

        private byte[] CraftSourceNameFromIndices(uint input, TypeCache.eGender gender)
        {
            // Drop the MSB from input, then convert it to a hex string.
            var raw = (input & 0x7FFFFFFF).ToString("X8");
            var sb = new StringBuilder(raw);
            for (int i = sb.Length - 2; i >= 0; i -= 2)
                sb.Insert(i, ' ');
            var tail = sb.ToString().TrimStart();

            // Replace the first 2 characters depending on gender.
            var newMsb = gender == TypeCache.eGender.Female ? FemaleSourcePrefix : MaleSourcePrefix;
            tail = newMsb + tail.Substring(2);

            return HexStringToBytes(tail);
        }
        
        private TypeCache.CoreObject GetActiveCoreObject()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.CharacterObject;
        }

        private Character GetActiveCharacter()
        {
            var msg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVECHARACTER();
            var response = AskSessionServices<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(msg);

            return response.Character;
        }

        // TODO: Move this to a utility class.
        private static byte[] HexStringToBytes(string str)
        {
            str = str.Replace(" ", "");
            if (str.Length % 2 != 0) throw new Exception("Hex string must have even number of characters");

            // Convert each pair of characters to a byte and add to the output array
            var ret = new byte[str.Length / 2];
            for (int i = 0; i < str.Length; i += 2)
            {
                var byteString = str.Substring(i, 2);
                var b = Convert.ToByte(byteString, 16);
                ret[i / 2] = b;
            }

            return ret;
        }

    }
}