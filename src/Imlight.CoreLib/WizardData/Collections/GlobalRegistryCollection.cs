/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models;
using Imlight.CoreLib.WizardData.Models.World;
using Raven.Client.Documents;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;

public static class GlobalRegistryCollection {

    private const string CollectionName = "GlobalRegistry";

    private static readonly IDocumentStore s_store;
    private static bool s_isInitialized;
    private static GlobalRegistryModel s_model;

    static GlobalRegistryCollection() => s_store = WorldDatabase.Instance.Store;

    /// <summary>
    /// Saves a new global registry to the database.
    /// </summary>
    /// <param name="globalRegistry"></param>
    public static void SaveGlobalRegistry(GlobalRegistryModel globalRegistry) {
        using var session = s_store.OpenSession();

        // Delete the old global registry.
        var oldGlobalRegistry = session
            .Query<GlobalRegistryModel>(collectionName: CollectionName)
            .FirstOrDefault();
        if (oldGlobalRegistry is not null) {
            session.Delete(oldGlobalRegistry);
        }

        // Store the new one and set it's metadata.
        session.Store(globalRegistry);
        var metadata = session.Advanced.GetMetadataFor(globalRegistry);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        s_model = globalRegistry;
        s_isInitialized = true;
    }

    /// <summary>
    /// Gets the global registry from the database.
    /// </summary>
    /// <returns></returns>
    public static GlobalRegistryModel GetGlobalRegistry() {
        if (s_isInitialized) {
            return s_model;
        }

        using var session = s_store.OpenSession();
        s_model = session
            .Query<GlobalRegistryModel>(collectionName: CollectionName)
            .FirstOrDefault();

        s_isInitialized = true;
        return s_model;
    }

    /// <summary>
    /// Gets a registry entry from the global registry.
    /// </summary>
    /// <returns></returns>
    public static float GetRegistryEntry(string entry) {
        if (!s_isInitialized) {
            s_model = GetGlobalRegistry();
        }

        if (s_model is null) {
            return 0;
        }

        if (!s_model.GlobalRegistryValues.ContainsKey(entry)) {
            Logger.Warning("Global registry entry {0} does not exist.", Logger.Args(entry));
            return 0;
        }

        return s_model.GlobalRegistryValues[entry];
    }

    /// <summary>
    /// Checks if the global registry requirements are met.
    /// </summary>
    /// <param name="values"> The requirements to check. </param>
    /// <param name="operatorType"> The operator type to use. </param>
    /// <returns> True if all requirements are met, false otherwise. </returns>
    public static bool CheckGlobalRegistryRequirements(List<Requirement> values, Requirement.Operator operatorType) {
        var allMatched = true;

        foreach (var requirement in values) {
            if (requirement is ReqGlobalRegistryValue globalReq) {
                if (!GlobalRegistryValueMet(globalReq)
                    && operatorType == Requirement.Operator.ROP_AND) {
                    return false;
                }

                allMatched = allMatched && !globalReq.m_applyNOT;
            }
            else {
                Logger.Warning("Holy!!! We found a spawn requirement that isn't a global registry value. " +
                            "This is a problem. Let Jooty know.");
            }
        }

        return allMatched;
    }

    private static bool GlobalRegistryValueMet(ReqGlobalRegistryValue value) {
        var globalValue = GetRegistryEntry(value.m_entryName);

        switch (value.m_operatorType) {
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_EQUALS:
                return value.m_numericValue == globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN:
                return value.m_numericValue < globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ:
                return value.m_numericValue <= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN:
                return value.m_numericValue > globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ:
                return value.m_numericValue >= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_UNKNOWN:
            default: {
                    Logger.Error("Zone contains a spawn requirement that " +
                                      "references a global registry value that does not exist. " +
                                      "Entry name: {EntryName}", Logger.Args(value.m_entryName));
                    return false;
                }
        }
    }

}
