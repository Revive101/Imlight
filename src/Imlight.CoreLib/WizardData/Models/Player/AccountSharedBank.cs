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
 * ACCOUNT SHARED BANK
 * ========================================================================
 *
 * PURPOSE:
 * The bank that every character of an account shares: the ids of its items, reagent stacks and pet snack
 * stacks, its capacity, and the loaded documents.
 *
 * USAGE EXAMPLE:
 * var shared = wizard.Account.SharedBank;
 *
 * NOTE:
 * The account document keeps only the ids. The items are documents of the WizardItems, WizardReagents and
 * WizardPetSnacks collections owned by the account id, loaded with the account. A session that shares the
 * account holds its own copy, so a move validates against the stored document, see BankCollection.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Bank;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class AccountSharedBank {

    public List<ulong> ItemIds { get; set; } = [];
    public List<ulong> ReagentIds { get; set; } = [];
    public List<ulong> SnackIds { get; set; } = [];

    /// <summary>
    /// The capacity this account has of its own. Zero follows the ini default, see <see cref="Limit"/>.
    /// </summary>
    public int ItemLimit { get; set; }

    [JsonIgnore] public List<WizClientObjectItem> Items { get; set; } = [];
    [JsonIgnore] public List<ClientReagentItem> Reagents { get; set; } = [];
    [JsonIgnore] public List<ClientPetSnackItem> Snacks { get; set; } = [];

    /// <summary>
    /// The most regular items the shared bank holds.
    /// </summary>
    [JsonIgnore] public int Limit => ItemLimit > 0 ? ItemLimit : BankConfig.MaxSharedBankItems;

}
