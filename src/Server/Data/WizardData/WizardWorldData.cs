/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Data.WizardData;

public static class WizardWorldData
{
    public const string StartingZone = "WizardCity/WC_Hub";
    public const ushort StartingLevel = 1;
    public const byte StartingWorld = 1;
    public const int GoldPouchMax = 1000000;

    // TODO: The fields below are temporary until we move them to a database.
    public const bool IsHalloween = false;
    public const string HalloweenObjectAdjectivePrefix = "HO";
    public const bool IsBirthday = false;
    public const string BirthdayObjectKeyword = "BDay";
    public const bool IsChristmas = false;
    public const string ChristmasObjectAdjectivePrefix = "CH";
    public static readonly WizardZoneEventData[] GlobalZoneEvents =
    {
        new()
        {
            Name = "Birthday",
            Description = "Wizard101 anniversary event",
            IsEnabled = false,
            EnabledByDefault = false,
            StartDate = new DateTime(2023, 09, 02),
            EndDate = new DateTime(2023, 09, 11),
            ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.Contains,
            ObjectAdjectiveWhitelist = { "BDay" },
        },
        new()
        {
            Name = "Halloween",
            Description = "Halloween event",
            IsEnabled = false,
            EnabledByDefault = false,
            StartDate = new DateTime(2023, 10, 01),
            EndDate = new DateTime(2023, 11, 01),
            ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.PrefixedWith,
            ObjectAdjectiveWhitelist = { "HO" },
        },
        new()
        {
            Name = "Christmas",
            Description = "Christmas event",
            IsEnabled = false,
            EnabledByDefault = false,
            StartDate = new DateTime(2023, 12, 01),
            EndDate = new DateTime(2023, 12, 31),
            ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.PrefixedWith,
            ObjectAdjectiveWhitelist = { "CH" },
        },
    };
    public static readonly WizardZoneData[] ZoneEvents =
    {
        new()
        {
            ZoneName = "WizardCity/WC_Hub",
            Events =
            {
                new WizardZoneEventData
                {
                    Name = "NewConstruction",
                    Description = "Rebuilding of Wizard City",
                    IsEnabled = false,
                    EnabledByDefault = false,
                    ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.Raw,
                    ObjectAdjectiveWhitelist =
                    {
                        "WC2_SurveyingStake", "WC-HUB-AmbientSaw", "WC-HUB-AmbientHammer",
                        "WC-HUB-NPC11", "WC-HUB-NPC12", "WC-HUB-NPC13"
                    },
                },
                new WizardZoneEventData
                {
                    Name = "NOFKD",
                    Description = "The bunny cookie guy.",
                    IsEnabled = false,
                    EnabledByDefault = false,
                    ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.Contains,
                    ObjectAdjectiveWhitelist = { "NOFKD", "WC-HUB-NPC19" },
                },
                new WizardZoneEventData
                {
                    Name = "Crown Furniture",
                    Description = "Shows presents in Wizard City hub.",
                    IsEnabled = false,
                    EnabledByDefault = false,
                    ObjectAdjectiveType = WizardZoneEventObjectAdjectiveType.Raw,
                    ObjectAdjectiveWhitelist = { "WC-CROWN-FURNITURE" },
                },
            }
        }
    };
    
    /// <summary>
    /// Checks if a given object is used for any global events and if the event is active.
    /// </summary>
    /// <param name="template"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static bool IsCoreObjectOfInactiveWorldEvent(GameObjectTemplate template)
    {
        // Iterate through each zone event in WorldStats.GlobalZoneEvents
        return GlobalZoneEvents
            .Where(x => !Util.IsDateTimeNowBetween(x.StartDate, x.EndDate) || !x.IsEnabled || !x.EnabledByDefault)
            .Any(e => IsCoreObjectOfEvent(template, e));
    }

    public static bool IsCoreObjectOfInactiveZoneEvent(GameObjectTemplate template, string zoneName)
    {
        if (ZoneEvents.All(z => z.ZoneName != zoneName))
            return false;
        
        var zone = ZoneEvents.First(z => z.ZoneName == zoneName);
        return zone.Events
            .Where(x => !Util.IsDateTimeNowBetween(x.StartDate, x.EndDate) || !x.IsEnabled || !x.EnabledByDefault)
            .Any(e => IsCoreObjectOfEvent(template, e));
    }

    private static bool IsCoreObjectOfEvent(GameObjectTemplate template, WizardZoneEventData e)
    {
        // Make all the adjectives lowercase for easier comparison.
        var lowerCaseAdjectives = e.ObjectAdjectiveWhitelist.Select(adj => adj.ToLower()).ToList();

        // Iterate through each adjective in the adjective list
        foreach (var adj in template.m_adjectiveList)
        {
            // Trim off the ".AdjRef" suffix, if one exists.
            var mutAdj = adj.ToString().ToLower();
            if (mutAdj.EndsWith(".adjref"))
                mutAdj = adj.ToString()[..(adj.ToString().Length - 7)].ToLower();
            
            // Check the ZoneEventObjectAdjectiveType to determine the matching condition
            switch (e.ObjectAdjectiveType)
            {
                case WizardZoneEventObjectAdjectiveType.Contains:
                    if (lowerCaseAdjectives.Any(mutAdj.Contains))
                        return true;
                    break;
                case WizardZoneEventObjectAdjectiveType.PrefixedWith:
                    if (lowerCaseAdjectives.Any(mutAdj.StartsWith))
                        return true;
                    break;
                case WizardZoneEventObjectAdjectiveType.SuffixedWith:
                    if (lowerCaseAdjectives.Any(mutAdj.EndsWith))
                        return true;
                    break;
                case WizardZoneEventObjectAdjectiveType.Raw:
                    if (lowerCaseAdjectives.Contains(mutAdj))
                        return true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(WizardZoneEventObjectAdjectiveType));
            }
        }

        return false;
    }
}