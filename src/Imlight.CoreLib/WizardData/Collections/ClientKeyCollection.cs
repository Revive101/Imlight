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
*/

using System;
using Raven.Client;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imcodec.IO;

namespace Imlight.CoreLib.WizardData.Collections;

public class ClientKeyPair(ulong accountId, ulong machineId, string clientKey2) {

    public ulong AccountId { get; set; } = accountId;
    public ulong MachineId { get; set; } = machineId;
    public string ClientKey2 { get; set; } = clientKey2 
        ?? throw new ArgumentNullException(nameof(clientKey2));

}

public static class ClientKeyCollection {

    private const string SESSION_KEY_COLLECTION_NAME = "SessionKeys";
    private const uint SESSION_KEY_LIFETIME_IN_HOURS = 30;

    private static readonly IDocumentStore Store;

    static ClientKeyCollection() {
        Store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Stores the account's session key, replacing the one from its last login.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="machineId"></param>
    /// <param name="key"></param>
    public static void AddSessionKey(ulong accountId, ulong machineId, string key) {
        using var session = Store.OpenSession();

        // A null change vector overwrites the last login's key in the same write, so validation never reads it.
        var pair = new ClientKeyPair(accountId, machineId, key);
        session.Store(pair, changeVector: null, SessionKeyDocumentId(accountId));

        var metadata = session.Advanced.GetMetadataFor(pair);
        metadata[Constants.Documents.Metadata.Collection] = SESSION_KEY_COLLECTION_NAME;
        metadata[Constants.Documents.Metadata.Expires] = DateTime.UtcNow.AddHours(SESSION_KEY_LIFETIME_IN_HOURS);

        session.SaveChanges();
    }

    /// <summary>
    /// Removes the session key stored for an account.
    /// </summary>
    /// <param name="accountId"></param>
    public static void RemoveSessionKeys(ulong accountId) {
        using var session = Store.OpenSession();
        session.Delete(SessionKeyDocumentId(accountId));
        session.SaveChanges();
    }

    /// <summary>
    /// Gets the session key the account was given on this machine, or null when it has none.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="machineId"></param>
    /// <returns></returns>
    public static ByteString GetSessionKey(ulong accountId, ulong machineId) {
        using var session = Store.OpenSession();

        var pair = session.Load<ClientKeyPair>(SessionKeyDocumentId(accountId));
        var keyForThisMachine = pair?.MachineId == machineId ? pair.ClientKey2 : null;

        return keyForThisMachine;
    }

    private static string SessionKeyDocumentId(ulong accountId)
        => $"{SESSION_KEY_COLLECTION_NAME}/{accountId}";

}
