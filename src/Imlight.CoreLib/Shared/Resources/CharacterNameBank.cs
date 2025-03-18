/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.IO;
using System.Xml;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Resources;

public class WizardNameBank : RootSingleResourceSingleton<WizardNameBank>, IMemoryStreamDisposable {

    protected override string ResourceName => "CharacterNames.xml";
    private const string CharacterLocaleTable = "CharacterNames";
    private const string FirstNameHumanMaleTableName = "FirstName_HumanMale";
    private const string FirstNameHumanFemaleTableName = "FirstName_HumanFemale";
    private const string MiddleNameHumanTableName = "MiddleName_Human";
    private const string LastNameHumanTableName = "LastName_Human";

    private static Dictionary<string, List<string>> s_characterNameTable;

    protected override void AfterLoad() {
        s_characterNameTable = GetCharacterNameTable(Stream);
        Logger.Information("Loaded {0} character name tables.", Logger.Args(s_characterNameTable.Count));
    }

    /// <summary>
    /// Retrieves the English name based on the given name indices and gender.
    /// </summary>
    /// <param name="nameIndices">The name indices containing the first name, middle name, and last name.</param>
    /// <param name="gender">The gender of the character.</param>
    /// <returns>The English name composed of the first name, middle name, and last name.</returns>
    public static string GetEnglishName(uint nameIndices, eGender gender) {
        // The first 8 bits are the first name, the next 8 bits are the middle name, and the last 8 bits are the last name.
        var firstNameIndex = (int) (nameIndices >> 16) & 0xFF;
        var middleNameIndex = (int) ((nameIndices >> 8) & 0xFF);
        var lastNameIndex = (int) (nameIndices & 0xFF);

        var firstNameTableName = (gender == eGender.Male) ? FirstNameHumanMaleTableName : FirstNameHumanFemaleTableName;
        var firstName = GetEnglishNamePart(firstNameTableName, firstNameIndex);

        var middleName = string.Empty;
        if (middleNameIndex != 0) {
            middleName = GetEnglishNamePart(MiddleNameHumanTableName, middleNameIndex);
        }

        var lastName = string.Empty;
        if (lastNameIndex != 0) {
            lastName = GetEnglishNamePart(LastNameHumanTableName, lastNameIndex);
        }

        if (middleNameIndex == 0 && lastNameIndex == 0) {
            // If the middle name and last name are both 0, then the first name is the full name.
            return firstName;
        }

        return $"{firstName} {middleName}{lastName}";
    }

    private static string GetEnglishNamePart(string tableName, int index) {
        if (!s_characterNameTable.TryGetValue(tableName, out var characterNames)) {
            return "[TABLE_NOT_FOUND]";
        }

        if (index >= characterNames.Count) {
            return "[INDEX_OUT_OF_RANGE]";
        }

        // The character name table is just a list of locale IDs.
        var localeNameid = characterNames[index];
        var englishName = Locale.GetEnglishName(CharacterLocaleTable, localeNameid) ?? $"[NAME_NOT_FOUND]";

        return englishName;
    }

    private static Dictionary<string, List<string>> GetCharacterNameTable(MemoryStream fileStream) {
        // Convert the file stream to an XML document.
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(fileStream);

        var result = new Dictionary<string, List<string>>();
        var tableNodes = xmlDocument.SelectNodes("/CharacterNameTable/Table");
        foreach (XmlNode tableNode in tableNodes) {
            // As of r766693 (11/15/2024), the game client now includes multiple character name tables for different locales.
            // Note that all recorded tables still reference the same, English lookup name.
            // In this case, we're only interested in the German locale since it's the first table.
            var localeAttr = tableNode.Attributes["Locale"];
            if (localeAttr != null && localeAttr.Value != "de") {
                continue;
            }

            string tableName = tableNode.Attributes["Name"].Value;
            List<string> characterNames = new List<string>();

            XmlNodeList characterNameNodes = tableNode.SelectNodes("CharacterName");
            foreach (XmlNode characterNameNode in characterNameNodes) {
                characterNames.Add(characterNameNode.InnerText);
            }

            result.Add(tableName, characterNames);
        }

        return result;
    }

    public void DisposeStream() 
        => Stream.Dispose();

}
