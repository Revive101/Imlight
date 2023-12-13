/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Resources;

public static class WizardNameBank {
    private const string EnglishCharacterNamesPath = "Locale/English/CharacterNames.lang";
    private const string CharacterNameTablePath = "CharacterNames.xml";
    private const string FirstNameHumanMaleTableName = "FirstName_HumanMale";
    private const string FirstNameHumanFemaleTableName = "FirstName_HumanFemale";
    private const string MiddleNameHumanTableName = "MiddleName_Human";
    private const string LastNameHumanTableName = "LastName_Human";

    private static readonly string[] _englishNameBank = GetEnglishNameBank();
    private static readonly Dictionary<string, List<string>> _characterNameTable = GetCharacterNameTable();

    /// <summary>
    /// Retrieves the English name based on the given name indices and gender.
    /// </summary>
    /// <param name="nameIndices">The name indices containing the first name, middle name, and last name.</param>
    /// <param name="gender">The gender of the character.</param>
    /// <returns>The English name composed of the first name, middle name, and last name.</returns>
    public static string GetEnglishName(uint nameIndices, eGender gender)
    {
        // Drop the uneeded MSB.
        nameIndices &= 0x7FFFFFFF;

        // The first 8 bits are the first name, the next 8 bits are the middle name, and the last 8 bits are the last name.
        var firstNameIndex = (int)(nameIndices >> 16);
        var middleNameIndex = (int)((nameIndices >> 8) & 0xFF);
        var lastNameIndex = (int)(nameIndices & 0xFF);

        var firstNameTableName = (gender == eGender.Male) ? FirstNameHumanMaleTableName : FirstNameHumanFemaleTableName;
        var firstName = GetEnglishNamePart(firstNameTableName, firstNameIndex);

        string middleName = string.Empty;
        if (middleNameIndex != 0)
        {
            middleName = GetEnglishNamePart(MiddleNameHumanTableName, middleNameIndex);
        }

        string lastName = string.Empty;
        if (lastNameIndex != 0)
        {
            lastName = GetEnglishNamePart(LastNameHumanTableName, lastNameIndex);
        }

        if (middleNameIndex == 0 && lastNameIndex == 0)
        {
            // If the middle name and last name are both 0, then the first name is the full name.
            return firstName;
        }

        return $"{firstName} {middleName}{lastName}";
    }

    private static string GetEnglishNamePart(string tableName, int index) {
        if (!_characterNameTable.TryGetValue(tableName, out var characterNames)) {
            return "[NOT_FOUND]";
        }

        if (index >= characterNames.Count) {
            return "[NOT_FOUND]";
        }

        var localeNameid = characterNames[index];

        // Search through the English name bank for the name.
        // Find the ID of the name. The actual name will be 2 lines below the ID.
        var realNameIdIndex = Array.IndexOf(_englishNameBank, _englishNameBank.First(x => x == localeNameid));
        if (realNameIdIndex == -1) {
            return "[NOT_FOUND]";
        }

        return _englishNameBank[realNameIdIndex + 2];
    }

    private static string[] GetEnglishNameBank() {
        if (!ResourceManager.TryLoadFile(ResourceManager.RootWadName, EnglishCharacterNamesPath, out var fileStream)) {
            throw new Exception(EnglishCharacterNamesPath);
        }

        // Convert file stream to a string array of lines.
        var lines = new List<string>();
        using (var reader = new StreamReader(fileStream)) {
            string line;
            while ((line = reader.ReadLine()) != null) {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    private static Dictionary<string, List<string>> GetCharacterNameTable() {
        if (!ResourceManager.TryLoadFile(ResourceManager.RootWadName, CharacterNameTablePath, out var fileStream)) {
            throw new Exception(EnglishCharacterNamesPath);
        }

        // Convert the file stream to an XML document.
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(fileStream);

        var result = new Dictionary<string, List<string>>();
        var tableNodes = xmlDocument.SelectNodes("/CharacterNameTable/Table");
        foreach (XmlNode tableNode in tableNodes) {
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
}
