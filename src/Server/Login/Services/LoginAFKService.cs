/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Configuration;
using WizUnraveler;
using WizUnraveler.Cache;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Login.Services;

internal class LoginAFKService : MessageService
{
    private readonly ushort _afkTimeout = ConfigurationManager.Settings.LoginAfkTimeout;
    private readonly ushort _afkCheckInterval = ConfigurationManager.Settings.LoginAfkCheckInterval;

    private bool _halted;
    private long _lastReceivedSeconds;
    private readonly Timer _timer;

    public LoginAFKService(SessionActor parentActor) : base (parentActor)
    {
        _timer = new Timer(_afkCheckInterval * 1000);
        _timer.Elapsed += CheckAfk;
        _timer.AutoReset = true;
        _timer.Enabled = true;
    }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new LoginAFKService(parentActor));
    }

    [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK))]
    private void ReceiveLoginNotAfk(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message)
    {
        _lastReceivedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT))]
    private void ReceiveHalt(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT message)
    {
        _halted = true;
    }
        
    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME))]
    private void ReceiveResume(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME message)
    {
        _halted = false;
    }

    private void CheckAfk(object sender, ElapsedEventArgs e)
    {
        if (_halted) return;
            
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (currentTime - _lastReceivedSeconds >= _afkTimeout)
        {
            // User has gone AFK. Drop the connection.
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_DISCONNECT_LOGIN_AFK()
            {
                Warning = 1 // ???
            });

            CloseSession();
        }
    }
}