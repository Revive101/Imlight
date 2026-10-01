using System.Reflection;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using Imcodec.Wad;

if (args.Length != 1) throw new ArgumentException("Usage: ResourceStartupCheck /path/to/Root.wad");
var config = Path.GetTempFileName();
try {
    File.WriteAllText(config, "[Logging]\nLogLevel=ERROR\nLogPath=" + config + ".log\nLogFormat={Message}{NewLine}\n[Character]\nMaxLevel=200\n");
    ConfigurationManager.Initialize(config);
    using var stream = File.OpenRead(args[0]);
    var wad = ArchiveParser.Parse(stream);
    typeof(CoreObjectFactory).Assembly.GetType("Imlight.CoreLib.Shared.Resources.RootArchiveLoader")!
        .GetField("s_rootWad", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, wad);

    // Exercise actual startup before the first runtime access to Instance.
    _ = new Imlight.Director.ResourceContainer();
    var resourceTypes = typeof(CoreObjectFactory).Assembly.GetTypes().Where(type => !type.IsAbstract
        && type.BaseType?.IsGenericType == true
        && (type.BaseType.GetGenericTypeDefinition() == typeof(RootSingleResourceSingleton<>)
            || type.BaseType.GetGenericTypeDefinition() == typeof(RootDirectoryResourceSingleton<>))).ToArray();
    var instances = resourceTypes.ToDictionary(type => type, GetInstance);
    _ = new Imlight.Director.ResourceContainer();
    foreach (var (type, instance) in instances) {
        if (instance.GetType() != type || !ReferenceEquals(instance, GetInstance(type)))
            throw new Exception($"Startup did not reuse {type.Name}'s own singleton.");
    }
    Console.WriteLine($"PASS: {resourceTypes.Length} production resource types initialize once and survive subsequent runtime access/repeated discovery.");
} finally {
    File.Delete(config);
    File.Delete(config + ".log");
}

static object GetInstance(Type type) => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.GetValue(null)!;
