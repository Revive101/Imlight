using System;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;

namespace Imlight.Patch
{
    public class PatchServer : Server
    {
        public const string DEFAULT_PATCH_SERVER_NAME = "Imlight.Patch";
        public const ushort DEFAULT_PATCH_SERVER_PORT = 12300;
        
        public PatchServer(string name, int port, Props factoryProps) : base(name, port, factoryProps)
        {
            Log.Logger.Information($"Patch server created with " +
                                   $"name {name} " +
                                   $"under port {port}.");
        }
        
        public static Props Props(
            string serverName = DEFAULT_PATCH_SERVER_NAME,
            ushort serverPort = DEFAULT_PATCH_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new PatchServer(serverName, serverPort, null));
        }
    }
}