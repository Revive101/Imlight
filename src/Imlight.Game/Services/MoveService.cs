using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;
using Imlight.Net.Messages;
using Imlight.Common;
using Imlight.Game;

namespace Imlight.Game.Services
{
    internal class MoveService : MessageService
    {
        public MoveService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new MoveService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE))]
        private void ReceiveClientMove(GAME_5_PROTOCOL.MSG_CLIENTMOVE message)
        {
            var account = GetSocketAccount();

            // If the socket account cannot be found, send the client an error.
            if (account is null)
            {
                Log.Logger.Error($"Service [{this.GetType()}] socket account could not be retrieved!");
                return;
            }

            var character = account.Characters[0]; //Bad
            var zone = character.CreationData.m_location;

            // @todo: check for zone transfer trigger locations

            // @todo: update character position for server
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE))]
        private void ReceiveClientMoveState(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE message)
        {
            // @todo: update move state for a character
        }

        // Assuming (for now) that no two zones are on top of each other, and collision can be checked by determining if player's position is within some (X, Y) area
        private static bool InsideOfSquare(SharpDX.Vector2 p1, SharpDX.Vector2 p2, SharpDX.Vector2 p3, SharpDX.Vector2 p4, SharpDX.Vector2 pos)
        {
            // @todo: matrix magic
            return false;
        }
    }
}
