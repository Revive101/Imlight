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
 * LOGIN AFK SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages user session timeouts by tracking client activity and 
 * automatically disconnecting inactive login sessions.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * 
 * TODO:
 * - Clarify the meaning of Warning = 1 in disconnect message
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Timers;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Login.Services;

internal class LoginAFKService : MessageService {

    private readonly ushort _afkTimeout 
        = ConfigurationManager.Settings["Login Server.LoginAfkTimeout"].AsUShort();
    private readonly ushort _afkCheckInterval 
        = ConfigurationManager.Settings["Login Server.LoginAfkCheckInterval"].AsUShort();

    private bool _halted;
    private long _lastReceivedSeconds;
    private readonly Timer _timer;

    public LoginAFKService(SessionActor parentActor) : base(parentActor) {
        _timer = new Timer(_afkCheckInterval * 1000);
        _timer.Elapsed += CheckAfk;
        _timer.AutoReset = true;
        _timer.Enabled = true;
    }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new LoginAFKService(parentActor));

    protected override void OnDispose() {
        base.OnDispose();

        CloseSession();
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK))]
    private void ReceiveLoginNotAfk(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message) {
        _lastReceivedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT))]
    private void ReceiveHalt(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT message) {
        _halted = true;
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME))]
    private void ReceiveResume(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME message) {
        _halted = false;
    }

    private void CheckAfk(object sender, ElapsedEventArgs e) {
        if (_halted) {
            return;
        }

        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (currentTime - _lastReceivedSeconds >= _afkTimeout) {
            // User has gone AFK. Drop the connection.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_DISCONNECT_LOGIN_AFK() {
                Warning = 1 // ???
            });

            CloseSession();
        }
    }
    
}
