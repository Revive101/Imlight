/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Threading.Tasks;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.Login.Models;
using Imlight.Server.Shared.WizardData.Models;
using Raven.Client.Documents;

namespace Imlight.Server.Shared.WizardData.Implementations;

public static class GlobalRegistry
{
    private const string CollectionName = "GlobalRegistry";
    private static readonly IDocumentStore Store;

    private static bool _isInitialized;
    private static GlobalRegistryModel _model;

    static GlobalRegistry()
    {
        Store = WorldDatabase.Instance.Store;
    }

    /// <summary>
    /// Saves a new global registry to the database.
    /// </summary>
    /// <param name="globalRegistry"></param>
    public static void SaveGlobalRegistry(GlobalRegistryModel globalRegistry)
    {
        using var session = Store.OpenSession();

        // Delete the old global registry.
        var oldGlobalRegistry = session
            .Query<GlobalRegistryModel>(collectionName: CollectionName)
            .FirstOrDefault();
        if (oldGlobalRegistry is not null)
            session.Delete(oldGlobalRegistry);

        // Store the new one and set it's metadata.
        session.Store(globalRegistry);
        var metadata = session.Advanced.GetMetadataFor(globalRegistry);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        _model = globalRegistry;
        _isInitialized = true;
    }

    /// <summary>
    /// Gets the global registry from the database.
    /// </summary>
    /// <returns></returns>
    public static GlobalRegistryModel GetGlobalRegistry()
    {
        if (_isInitialized)
            return _model;

        using var session = Store.OpenSession();
        _model = session
            .Query<GlobalRegistryModel>(collectionName: CollectionName)
            .FirstOrDefault();

        _isInitialized = true;
        return _model;
    }

    /// <summary>
    /// Gets a registry entry from the global registry.
    /// </summary>
    /// <returns></returns>
    public static float GetRegistryEntry(string entry)
    {
        if (!_isInitialized)
            _model = GetGlobalRegistry();
        if (_model is null)
            return 0;
        if (!_model.GlobalRegistryValues.ContainsKey(entry))
        {
            Log.Warning("Global registry entry {0} does not exist.", Log.Args(entry));
            return 0;
        }

        return _model.GlobalRegistryValues[entry];
    }
}