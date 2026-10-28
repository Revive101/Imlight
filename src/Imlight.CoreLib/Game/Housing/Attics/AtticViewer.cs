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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * Holds one session service's view of its character's attic, so the services that need the attic count themselves
 * in once and out when the session ends.
 *
 * USAGE EXAMPLE:
 * if (_viewer.TryEnter(wizard, out var attic, out var opening)) {
 *     // send opening, use attic
 * }
 * _viewer.Release();
 *
 * NOTE:
 * - Each service that needs the attic owns its own viewer. The registry counts viewers.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.MessageLayer;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Housing.Attics;

internal sealed class AtticViewer {

    private AtticState _attic;

    /// <summary>
    /// Gets the character's attic, counting this viewer in the first time.
    /// </summary>
    /// <param name="wizard">The attic's owner.</param>
    /// <param name="attic">The live attic when this returns <c>true</c>.</param>
    /// <param name="opening">What the client is told when the attic becomes live; empty when this viewer had it already.</param>
    /// <returns><c>false</c> when the attic cannot be loaded or stored.</returns>
    internal bool TryEnter(Wizard wizard, out AtticState attic, out IReadOnlyList<IMessage> opening) {
        opening = [];
        attic = _attic;
        if (attic is not null) {
            return true;
        }

        if (!AtticRegistry.TryEnter(wizard, out attic, out var created, out var audit)) {
            Logger.Error("Character {CharId} has an attic that cannot be loaded.", Logger.Args(wizard.CharId));

            return false;
        }

        _attic = attic;
        opening = AtticMessages.Opening(wizard, attic, created, audit);

        return true;
    }

    /// <summary>
    /// Gets the character's attic and sends the client what it needs to know when the attic becomes live.
    /// </summary>
    /// <param name="wizard">The attic's owner.</param>
    /// <param name="send">Sends a message to the owner's client.</param>
    /// <param name="inform">Tells the owner something in the chat window.</param>
    /// <param name="attic">The live attic when this returns <c>true</c>.</param>
    /// <returns><c>false</c> when the attic cannot be loaded or stored; the owner has been told.</returns>
    internal bool TryOpen(Wizard wizard, Action<IMessage> send, Action<string> inform, out AtticState attic) {
        if (!TryEnter(wizard, out attic, out var opening)) {
            inform("Your attic is not available right now. Please try again later.");

            return false;
        }

        foreach (var message in opening) {
            send(message);
        }

        return true;
    }

    /// <summary>
    /// Counts this viewer out of the attic, if it was counted in.
    /// </summary>
    internal void Release() {
        if (_attic is null) {
            return;
        }

        AtticRegistry.Release(_attic);
        _attic = null;
    }

}
