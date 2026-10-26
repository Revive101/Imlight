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
 * BANK CONFIG
 * ========================================================================
 *
 * PURPOSE:
 * Reads the bank settings of Imlight.ini: the default capacities of a character's bank and of an account's
 * shared bank, the shared jewel cap and the two settings of an
 * expansion.
 *
 * USAGE EXAMPLE:
 * if (BankConfig.IsEnabled) { ... }
 *
 * NOTE:
 * The capacities have no default. While [Character] MaxBankItems or MaxSharedBankItems is unset or 0 or less,
 * IsEnabled is false and nothing offers the bank.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Threading;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Bank;

/// <summary>
/// Settings of the bank, read from the [Character] and [Bank] sections of Imlight.ini.
/// </summary>
internal static class BankConfig {

    private const string MAX_BANK_ITEMS_KEY = "Character.MaxBankItems";
    private const string MAX_SHARED_BANK_ITEMS_KEY = "Character.MaxSharedBankItems";
    private const string SHARED_JEWEL_CAP_KEY = "Bank.SharedJewelCap";
    private const string EXPAND_ELIXIR_AMOUNT_KEY = "Bank.ExpandElixirAmount";
    private const string MAX_BANK_LIMIT_KEY = "Bank.MaxBankItems";

    private static int s_warnedDisabled;

    internal static int MaxBankItems 
        => Math.Max(0, ConfigurationManager.GetValue(MAX_BANK_ITEMS_KEY, 0));
    internal static int MaxSharedBankItems
        => Math.Max(0, ConfigurationManager.GetValue(MAX_SHARED_BANK_ITEMS_KEY, 0));
    internal static int SharedJewelCap
        => Math.Max(0, ConfigurationManager.GetValue(SHARED_JEWEL_CAP_KEY, 0));
    internal static int ExpandElixirAmount
        => Math.Max(0, ConfigurationManager.GetValue(EXPAND_ELIXIR_AMOUNT_KEY, 0));
    internal static int MaxBankLimit 
        => Math.Max(0, ConfigurationManager.GetValue(MAX_BANK_LIMIT_KEY, 0));

    /// <summary>
    /// Whether the bank is on: both default capacities are set above 0.
    /// </summary>
    internal static bool IsEnabled {
        get {
            var maxBankItems = MaxBankItems;
            var maxSharedBankItems = MaxSharedBankItems;
            if (maxBankItems > 0 && maxSharedBankItems > 0) {
                return true;
            }

            if (Interlocked.Exchange(ref s_warnedDisabled, 1) == 0) {
                Logger.Warning("[Character] MaxBankItems and MaxSharedBankItems must both be set above 0, " +
                    "so the bank is not offered. MaxBankItems={Bank}, MaxSharedBankItems={Shared}.",
                    Logger.Args(maxBankItems, maxSharedBankItems));
            }

            return false;
        }
    }

}
