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
 * BANK TYPES
 * ========================================================================
 *
 * PURPOSE:
 * The vocabulary of the bank: the three containers an item can be in, the kinds of items the bank tells
 * apart, where a new item can end up and the reasons a request is denied.
 *
 * USAGE EXAMPLE:
 * var denial = BankRules.CanMove(BankContainer.Backpack, BankContainer.SharedBank, item, 1, wizard, account);
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

namespace Imlight.CoreLib.Game.Bank;

/// <summary>
/// The places an item can be in for the bank window.
/// </summary>
internal enum BankContainer {

    Backpack,
    Bank,
    SharedBank,

}

/// <summary>
/// The kinds of items the bank tells apart. Reagents and pet snacks are stacks, jewels have their own cap in
/// the shared bank.
/// </summary>
internal enum BankItemKind {

    Item,
    Jewel,
    Reagent,
    PetSnack,

}

/// <summary>
/// Where a new item ended up.
/// </summary>
internal enum Placement {

    Backpack,
    Bank,
    SharedBank,
    Lost,

}

/// <summary>
/// Why a bank request is refused.
/// </summary>
internal enum BankDenial {

    None,
    NotOpen,
    UnknownItem,
    BackpackFull,
    BankFull,
    SharedBankFull,
    SharedJewelsFull,
    NoTrade,
    SocketedJewels,
    House,
    HasContents,
    BadQuantity,
    Locked,
    WrongContainer,
    SaveFailed,
    Rental,

}
