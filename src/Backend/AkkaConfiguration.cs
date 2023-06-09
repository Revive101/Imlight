using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using Akka;
using Akka.Actor;
using Akka.Configuration;
using Imlight.Common.Utilities;

namespace Imlight.Backend
{
    internal static class AkkaConfiguration
    {
        private const string CONFIGURATION_FILE_NAME = "akka.conf";
        
        private static readonly string ConfigLocation = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
            $@"config/{CONFIGURATION_FILE_NAME}");

        internal static bool CreateActorSystem(string name, out ActorSystem system)
        {
            system = null;

            Log.Logger.Information($"Searching for Akka.NET configuration file [{ConfigLocation}]");
            
            if (!GetAkkaConfiguration(out var config))
                return false;

            system = ActorSystem.Create(name, config);
            return true;
        }

        private static bool GetAkkaConfiguration(out Config config)
        {
            config = default;

            try
            {
                if (!File.Exists(ConfigLocation))
                    return false;

                var configContents = File.ReadAllText(ConfigLocation);

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
