using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Internals.DML;

namespace Imlight.Internals
{
    public static class ProtocolDispatcher
    {

        private static Dictionary<byte, INetworkProtocol> _protocols = new Dictionary<byte, INetworkProtocol>()
        {
            { 0, new ControlMessages() },
            { 1, new SYSTEM_1_PROTOCOL() },
            { 2, new EXTENDEDBASE_2_PROTOCOL() },
            { 5, new GAME_5_PROTOCOL() },
            { 7, new LOGIN_7_PROTOCOL() },
            { 8, new PATCH_8_PROTOCOL() },
            { 9, new PET_9_PROTOCOL() },
            { 10, new SCRIPT_10_PROTOCOL() },
            { 11, new TESTMANAGER_11_PROTOCOL() },
            { 12, new WIZARD_12_PROTOCOL() },
            { 15, new MOVEBEHAVIOR_15_PROTOCOL() },
            { 16, new PHYSICS_16_PROTOCOL() },
            { 19, new AISCLIENT_19_PROTOCOL() },
            { 25, new SOBLOCKS_MESSAGES_25_PROTOCOL() },
            { 40, new SKULLRIDERS_MESSAGES_40_PROTOCOL() },
            { 41, new DOODLEDOUG_MESSAGES_41_PROTOCOL() },
            { 42, new MG1_MESSAGES_42_PROTOCOL() },
            { 43, new MG2_MESSAGES_43_PROTOCOL() },
            { 44, new MG3_MESSAGES_44_PROTOCOL() },
            { 45, new MG4_MESSAGES_45_PROTOCOL() },
            { 46, new MG5_MESSAGES_46_PROTOCOL() },
            { 47, new MG6_MESSAGES_47_PROTOCOL() },
            { 50, new WIZARDHOUSING_50_PROTOCOL() },
            { 51, new WIZARDCOMBAT_MESSAGES_51_PROTOCOL() },
            { 52, new QUEST_MESSAGES_52_PROTOCOL() },
            { 54, new MG9_MESSAGES_54_PROTOCOL() },
        };

        public static INetworkProtocol Dispatch(byte serviceID)
        {
            if (!_protocols.ContainsKey(serviceID))
                throw new InvalidOperationException($"No protocol by service ID [{serviceID}] found!");

            return _protocols[serviceID];
        }

    }
}
