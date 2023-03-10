using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Akka;
using Akka.Actor;
using Akka.Configuration;
using Imlight.Common;

namespace Imlight.Backend
{
    internal static class AkkaConfiguration
    {
        internal static readonly string _configFileLocation = @$"{Directory.GetCurrentDirectory()}\config\akka.conf";

        internal static bool CreateActorSystem(string name, out ActorSystem system)
        {
            system = null;

            if (!GetAkkaConfiguration(out var config))
                return false;

            system = ActorSystem.Create(name, config);
            return true;
        }

        internal static bool GetAkkaConfiguration(out Config config)
        {
            config = default;

            try
            {
                if (!File.Exists(_configFileLocation))
                    return false;

                var configContents = File.ReadAllText(_configFileLocation);

                config = ConfigurationFactory.ParseString(configContents);
                return true;
            }
            catch (Exception e)
            {
                Log.Logger.Error(e.Message);
                return false;
            }
        }
    }
}
