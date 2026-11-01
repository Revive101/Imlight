/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Concurrent;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class GlobalRegistryCollection {

    private static readonly ConcurrentDictionary<string, byte> s_reportedMissingEntries = new();

    /// <summary>
    /// Gets the global registry from SpiralDB.
    /// </summary>
    public static GlobalRegistryModel GetGlobalRegistry() 
        => SpiralDB.GlobalRegistry;

    /// <summary>
    /// Gets a registry entry from the global registry.
    /// </summary>
    public static float GetRegistryEntry(string entry) {
        var model = SpiralDB.GlobalRegistry;

        if (model is null) {
            return 0;
        }

        if (!model.GlobalRegistryValues.ContainsKey(entry)) {
            if (s_reportedMissingEntries.TryAdd(entry, 0)) {
                Logger.Warning("Global registry entry {Entry} is not in SpiralDB, so it reads as 0. This is logged once.",
                    Logger.Args(entry));
            }

            return 0;
        }

        return model.GlobalRegistryValues[entry];
    }

    /// <summary>
    /// Checks whether a single global registry requirement's value matches. The
    /// AND/OR/NOT semantics are applied by RequirementDispatcher, the single
    /// requirement evaluation engine.
    /// </summary>
    /// <param name="value"> The requirement to check. </param>
    /// <returns> True if the value matches its operator, false otherwise. </returns>
    public static bool GlobalRegistryValueMet(ReqGlobalRegistryValue value) {
        var globalValue = GetRegistryEntry(value.m_entryName);

        switch (value.m_operatorType) {
            case OPERATOR_TYPE.OPERATOR_EQUALS:
                return value.m_numericValue == globalValue;
            case OPERATOR_TYPE.OPERATOR_LESS_THAN:
                return value.m_numericValue < globalValue;
            case OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ:
                return value.m_numericValue <= globalValue;
            case OPERATOR_TYPE.OPERATOR_GREATER_THAN:
                return value.m_numericValue > globalValue;
            case OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ:
                return value.m_numericValue >= globalValue;
            default: {
                    Logger.Error("A global registry requirement on {EntryName} uses operator {Operator}, which the server does not support, so it is not met.",
                        Logger.Args(value.m_entryName, value.m_operatorType));

                    return false;
                }
        }
    }

}
