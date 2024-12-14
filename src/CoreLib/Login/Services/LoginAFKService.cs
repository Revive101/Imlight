/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Timers;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Login.Services;

internal class LoginAFKService : MessageService {
    private readonly ushort _afkTimeout = ConfigurationManager.Settings.LoginAfkTimeout;
    private readonly ushort _afkCheckInterval = ConfigurationManager.Settings.LoginAfkCheckInterval;

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
