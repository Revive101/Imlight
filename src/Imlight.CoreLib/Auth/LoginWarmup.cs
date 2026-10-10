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
 * LOGIN WARMUP
 * ========================================================================
 *
 * PURPOSE:
 * Runs the authentication, validation and character list work once at boot, with reads only,
 * so the first real login does not pay for first-use compilation and index creation.
 *
 * USAGE EXAMPLE:
 * The Director calls LoginWarmup.Run() once the database is up and before it logs that
 * Imlight may be connected to.
 *
 * NOTE:
 * Never throws and never waits longer than the timeout. It writes nothing, apart from deleting
 * the throwaway account that earlier versions left behind.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/10/2026
 */

using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Cryptography;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Auth;

public static class LoginWarmup {

    private const string WarmupUsername = "login-warmup";
    private const string WarmupIp = "127.0.0.1";
    private const ulong WarmupAccountId = 0;
    private const ulong WarmupMachineId = 1;
    private const ushort WarmupSessionId = 1;
    private const uint WarmupOfferSeconds = 1;
    private const uint WarmupOfferMilliseconds = 1;

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Warms the login path. Logs how long it took, or why it failed. Returns when it has finished
    /// or after the timeout, whichever comes first.
    /// </summary>
    public static void Run() {
        var stopwatch = Stopwatch.StartNew();
        try {
            var warmup = Task.Run(Warm);
            if (!warmup.Wait(s_timeout)) {
                Logger.Warning("Login warmup did not finish within {Seconds} s, booting on.",
                    Logger.Args(s_timeout.TotalSeconds));

                return;
            }

            Logger.Information("Login warmup completed in {Elapsed} ms.", Logger.Args(stopwatch.ElapsedMilliseconds));
        } catch (Exception ex) {
            Logger.Warning("Login warmup failed after {Elapsed} ms. {Exception}",
                Logger.Args(stopwatch.ElapsedMilliseconds, ex.GetBaseException().Message));
        }
    }

    private static void Warm() {
        RemoveLeftoverWarmupAccount();

        var password = Guid.NewGuid().ToString("N");
        var account = WarmAuthenticate(password);
        WarmValidate();
        WarmCharacterList(account);
    }

    private static Account WarmAuthenticate(string password) {
        var clientKey = ClientKey.HaskCK1(password, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        var rec1 = Rec1.Encode(Encoding.ASCII.GetBytes($"{WarmupSessionId} {WarmupUsername} {clientKey}"),
            WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        _ = Rec1.Decode(rec1, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);

        var account = AccountCollection.GetAccountForLoginWarmup();
        _ = InfractionCollection.IsMachineBanned(WarmupMachineId);
        _ = InfractionCollection.IsIpBanned(WarmupIp);
        _ = account.InfractionHistory.IsCurrentlyBanned;
        _ = ClientKey.VerifyCK1(DatabaseUtilities.CreateHashedPassword(password), WarmupSessionId,
            WarmupOfferSeconds, WarmupOfferMilliseconds, clientKey);

        var sessionKey = ClientKey.HashSessionKey(WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        _ = Rec1.Encode(sessionKey, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);

        return account;
    }

    private static void WarmValidate() {
        _ = AccountCollection.GetAccountForCharacterList(WarmupAccountId);
        _ = ClientKeyCollection.GetSessionKey(WarmupAccountId, WarmupMachineId);
        _ = PassKey3.VerifyPK3(WarmupUsername, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds, WarmupUsername);
    }

    private static void WarmCharacterList(Account account) {
        var serializer = new ObjectSerializer(Behaviors: SerializerFlags.None, Versionable: false);
        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        foreach (var character in account.Characters) {
            _ = serializer.Serialize(CharacterHelper.GetLoginScreenInfo(character), flags, out ByteString _);
        }

        _ = serializer.Serialize(CharacterHelper.GetLoginScreenInfo(CreateWarmupWizard()), flags, out ByteString _);
        _ = WizardItemCollection.TryGetWizardInventory(0, out _);
    }

    private static void RemoveLeftoverWarmupAccount() {
        // Earlier versions logged in as this account and deleted it again, which a killed boot could leave behind.
        var leftover = AccountCollection.GetAccountForCharacterList(WarmupUsername);
        if (leftover is null) {
            return;
        }

        Logger.Information("Deleting the {Username} account that an earlier boot left behind.", Logger.Args(WarmupUsername));
        ClientKeyCollection.RemoveSessionKeys(leftover.AccountId);
        AccountCollection.DeleteAccount(WarmupUsername);
    }

    private static Wizard CreateWarmupWizard()
        => new() {
            CharId = RandomGen.GenerateGUID(),
            Zone = "WizardCity/WC_Ravenwood",
            ZoneDisplayName = "Ravenwood",
            LastLoginTime = (uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            WizardAvatar = new WizardCharacterBehavior(),
            PlayerNameBehavior = new ServerWizPlayerNameBehavior { NameOverride = "Warmup" },
            MagicSchoolBehavior = new ServerMagicSchoolBehavior { MagicSchool = MagicSchool.Fire, Level = 1 },
            EquipmentBehavior = new ServerWizEquipmentBehavior { SlotList = [], EquippedItemIds = [], EquippedItems = [] },
            InventoryBehavior = new ServerWizInventoryBehavior { Items = [], InventoryItemIds = [] },
            AlchemyBehavior = new ServerAlchemyBehavior { Reagents = [], Recipes = [], CraftingSlots = [], ReagentItemIds = [] },
            QuestBehavior = new ServerQuestBehavior(),
            FriendsBehavior = new ServerFriendBehavior(),
            GameStats = new ServerWizGameStats(MagicSchool.Fire, 1),
            PetOwnerBehavior = new ServerPetOwnerBehavior { MaxSlots = 1 },
        };

}
