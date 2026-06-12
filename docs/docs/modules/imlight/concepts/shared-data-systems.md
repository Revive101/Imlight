# Shared Data Systems

## Overview

Imlight implements the game's original shared data architecture, where client and server load identical archives. The most critical of these shared archives is `Root.wad`, which contains global game configuration data that both client and server must understand.

::: warning
Imlight does not actively enforce data integrity between the client and server by default. You can enable this flag in `Imlight.ini`: EnforceRevision = true. If true, clients will be disconnected if their `Root.wad` revision does not match the server's.
:::

## Shared Archive Architecture

Both client and server share not only `Root.wad` but all zone archives, ensuring identical understanding of:
- Object templates and properties
- Zone layouts and content
- Spell, item, and NPC definitions

## Shared Data Access

Imlight has two different systems for accessing shared data:
1. The locale wad cache
2. The patch server

The first will always be checked first, and the second will be used as a fallback if the locale wad cache does not have the required archive. 

### Updating Game Revision

Imlight allows you to specify a game revision in `Imlight.ini`. If you still contain a `cache` file in your project directory, Imlight will automatically determine if your cached assets are out of date and download the latest versions from the patch server. This happens once on startup.

### Initialization Options

For now, Imlight will only download new archives as they are needed. Your players may experience a slight delay the first time they enter a new zone while the server downloads the required zone archive. In the future, Imlight may support preloading all archives on startup.

## Root.wad - The Foundation

`Root.wad` contains the core configuration data that drives the entire game:

```
Root.wad (Shared Client/Server Data)
├── CantripXPConfig.xml     → CantripFactory
├── StateData/              → StateFactory  
├── Sigils/                 → SigilFactory
├── SpellTemplates/         → SpellFactory
└── ...
```

For example, when a player casts a spell, both client and server reference the same spell template from Root.wad, ensuring they agree on requirements, effects, and behavior.

::: info
Interestingly, the server never even sends damage data over the network. Instead, the client just assumes the server will apply the same damage calculation based on the shared spell template. 

Of course, you can't use this to cheat. The server will still send the final health value back to the client on the next round, which will correct any discrepancies.
:::

## Factory System Abstractions

Imlight provides two base patterns for creating systems that load data from Root.wad.

To add a new data-driven system:

1. **Choose base class**: `RootSingleResourceSingleton<T>` or `RootDirectoryResourceSingleton<T>`
2. **Specify data source**: Override `ResourceName` or `DirectoryName`
3. **Implement loading**: Override `AfterLoad()` to deserialize and cache data
4. **Provide API**: Create static methods for accessing loaded data

### Single File Factories

For systems that load one configuration file:

```csharp
public class CantripFactory : RootSingleResourceSingleton<CantripFactory> {
    protected override string ResourceName => "CantripXPConfig.xml";
    private readonly static List<SpellTemplate> _myTemplateCache = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        serializer.Deserialize(Stream.ToArray(), out CantripConfig config);

        _myTemplateCache.AddRange(config.Templates);
    }
    
    public static CantripsSpellTemplate CreateCantripTemplateFromId(uint templateId) {
        var template = _myTemplateCache
            .FirstOrDefault(t => t.Id == templateId);

        return template as CantripsSpellTemplate;
    }
}
```

### Directory-Based Factories

For systems that load multiple related files:

```csharp
public class StateFactory : RootDirectoryResourceSingleton<StateFactory> {
    protected override string DirectoryName => "StateData/";
    
    private static readonly Dictionary<string, ObjStateSet> s_objectStateSets = [];
    
    protected override void AfterLoad() {
        foreach (var file in base.Files) {
            // Load and cache all state files from directory
        }
    }
    
    internal static ObjStateSet GetStateSet(string setName) {
        return s_objectStateSets.GetValueOrDefault(setName);
    }
}
```