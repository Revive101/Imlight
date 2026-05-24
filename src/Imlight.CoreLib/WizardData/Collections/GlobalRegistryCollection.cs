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

using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class GlobalRegistryCollection {

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
            Logger.Warning("Global registry entry {0} does not exist.",
                Logger.Args(entry));

            return 0;
        }

        return model.GlobalRegistryValues[entry];
    }

    /// <summary>
    /// Checks if the global registry requirements are met.
    /// </summary>
    /// <param name="values"> The requirements to check. </param>
    /// <param name="operatorType"> The operator type to use. </param>
    /// <returns> True if all requirements are met, false otherwise. </returns>
    public static bool CheckGlobalRegistryRequirements(RequirementList list) {
        var allMatched = true;
        var values = list.m_requirements;
        var operatorType = list.m_operator;

        foreach (var requirement in values) {
            if (requirement is ReqGlobalRegistryValue globalReq) {
                if (!GlobalRegistryValueMet(globalReq)
                    && operatorType == Operator.ROP_AND) {
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
                    Logger.Error("Zone contains a spawn requirement that " +
                                      "references a global registry value that does not exist. " +
                                      "Entry name: {EntryName}", Logger.Args(value.m_entryName));

                    return false;
                }
        }
    }

}
