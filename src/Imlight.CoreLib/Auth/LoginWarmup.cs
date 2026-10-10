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
 * Runs the authentication and character list work once at boot, on a throwaway account,
 * so the first real login does not pay for first-use compilation and index creation.
 *
 * USAGE EXAMPLE:
 * The Director calls LoginWarmup.Run() once the database is up and before it logs that
 * Imlight may be connected to.
 *
 * NOTE:
 * Never throws and never waits longer than the timeout. The throwaway account is deleted
 * again, including one left behind by a boot that was killed halfway.
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
using System.Threading;
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
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Auth;

public static class LoginWarmup {

    private const string WarmupUsername = "login-warmup";
    private const string WarmupIp = "127.0.0.1";
    private const ulong WarmupMachineId = 1;
    private const ushort WarmupSessionId = 1;
    private const uint WarmupOfferSeconds = 1;
    private const uint WarmupOfferMilliseconds = 1;
    private const int IndexRetryCount = 50;
    private const int IndexRetryDelayMilliseconds = 100;

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
        DeleteWarmupAccount();

        var password = Guid.NewGuid().ToString("N");
        var wizard = CreateWarmupWizard();
        var account = new Account(WarmupUsername, "login-warmup@invalid", DatabaseUtilities.CreateHashedPassword(password));
        wizard.AccountId = account.AccountId;
        account.CharacterIds.Add(wizard.CharId);
        account.Characters.Add(wizard);
        if (!AccountCollection.CreateAccount(account)) {
            return;
        }

        try {
            var loadedAccount = WarmAuthenticate(password);
            WarmCharacterList(loadedAccount);
        } finally {
            ClientKeyCollection.RemoveSessionKeys(account.AccountId);
            OnlinePlayerCollection.RemoveOnlinePlayer(account.AccountId);
            DeleteWarmupAccount();
        }
    }

    private static Account WarmAuthenticate(string password) {
        var clientKey = ClientKey.HaskCK1(password, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        var rec1 = Rec1.Encode(Encoding.ASCII.GetBytes($"{WarmupSessionId} {WarmupUsername} {clientKey}"),
            WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        _ = Rec1.Decode(rec1, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);

        // The account was written a moment ago, so its index may not list it yet.
        Account account = null;
        for (var attempt = 0; account is null && attempt < IndexRetryCount; attempt++) {
            account = AccountCollection.GetAccount(WarmupUsername);
            if (account is null) {
                Thread.Sleep(IndexRetryDelayMilliseconds);
            }
        }

        if (account is null) {
            throw new InvalidOperationException("The warmup account never became visible.");
        }

        _ = InfractionCollection.IsMachineBanned(WarmupMachineId);
        _ = InfractionCollection.IsIpBanned(WarmupIp);
        _ = account.InfractionHistory.IsCurrentlyBanned;
        _ = ClientKey.VerifyCK1(account.PasswordHash, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds, clientKey);

        var sessionKey = ClientKey.HashSessionKey(WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);
        ClientKeyCollection.AddSessionKey(account.AccountId, WarmupMachineId, sessionKey);
        _ = Rec1.Encode(sessionKey, WarmupSessionId, WarmupOfferSeconds, WarmupOfferMilliseconds);

        OnlinePlayerCollection.AddOnlinePlayer(new OnlinePlayer {
            SessionId = WarmupSessionId,
            AccountId = account.AccountId,
            CurrentRealm = "LoginServer",
            ActorPath = "warmup",
        });

        return account;
    }

    private static void WarmCharacterList(Account account) {
        var serializer = new ObjectSerializer(Behaviors: SerializerFlags.None, Versionable: false);
        var flags = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;
        foreach (var character in account.Characters) {
            _ = serializer.Serialize(CharacterHelper.GetLoginScreenInfo(character), flags, out ByteString _);
        }

        _ = WizardItemCollection.TryGetWizardInventory(0, out _);
    }

    private static void DeleteWarmupAccount() {
        // The delete looks the account up through an index that may lag behind the write.
        for (var attempt = 0; attempt < IndexRetryCount; attempt++) {
            if (AccountCollection.DeleteAccount(WarmupUsername)) {
                return;
            }

            Thread.Sleep(IndexRetryDelayMilliseconds);
        }
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
