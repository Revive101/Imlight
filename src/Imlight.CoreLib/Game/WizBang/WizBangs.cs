/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * WIZBANGS
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized loading and validation of WizBang templates
 * as they appear in the Root.wad
 * 
 * USAGE EXAMPLE:
 * var exists = WizBangFactory.DoesWizBangExist("CompleteQuestGoal");
 * 
 * NOTE:
 * WizBangs are the icon that usually appear over the head of an NPC
 * to indiciate shopkeeper status, quest status, etc.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Linq;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.WizBang;

public enum WizBangs {

    None = 0,
    CompleteQuestGoal = 432425611,
    StartQuest = 660791182,
    UnfinishedQuest = 747193091,
    Training = 382045279,
    Shopping = 388577048,
    Banking = 1488236563,
    Registrar = 756401294,
    NumberZero = 544837288,
    NumberOne = 1617983989,
    NumberTwo = 1603243205,
    NumberThree = 1586704701,
    NumberFour = 544877055,
    NumberFive = 544787151,
    NumberSix = 1617978570,
    NumberSeven = 1631636138,
    NumberEight = 517375025,
    NumberNine = 544789197,
    NumberTen = 1603243349,
    NumberEleven = 965821977,
    NumberTwelve = 2086536901,
    NumberThirteen = 743819941,
    NumberFourteen = 1403660647,
    NumberFifteen = 470877493,
    LeaderBoard = 560533500,
    CastleTours = 1884466314,
    SellFish = 1346845383,
    DailyQuest = 666762553,
    WhirlyBurly = 931528087,
    RateMyStitch = 1442124734

}

/// <summary>
/// Manages the loading, validation, and lookup of WizBang templates.
/// </summary>
internal class WizBangFactory : RootSingleResourceSingleton<WizBangFactory>, IMemoryStreamDisposable {

    protected override string ResourceName => "WizBangs.xml";

    private static WizBangTemplateManager s_templateManager;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();

        if (!serializer.Deserialize(base.Stream.ToArray(), 1, out s_templateManager)) {
            Logger.Error("Could not deserialize {0} as {1}", 
                Logger.Args(ResourceName, nameof(WizBangTemplateManager)));

            return;
        }

        // Iterate through the WizBangs enum and ensure that all of them are valid and present
        // in the template manager.
        foreach (var wizBang in Enum.GetValues<WizBangs>().Cast<WizBangs>()) {
            if (wizBang == WizBangs.None) {
                continue;
            }

            var matchOrNull = s_templateManager.m_templates.FirstOrDefault(x => x.m_name == wizBang.ToString());
            if (matchOrNull is not null) {
                // Ensure that the hash of the wizBang matches the hash in the template manager.
                var matchedHash = StringHash.Compute(matchOrNull.m_name);
                var wizBangHash = (uint) wizBang;
                if (matchedHash != wizBangHash) {
                    Logger.Error("WizBang {0} hash mismatch: {1} != {2}", 
                        Logger.Args(wizBang.ToString(), matchedHash, wizBangHash));
                }
            }
            else {
                Logger.Error("WizBang {0} not found in template manager.", 
                    Logger.Args(wizBang.ToString()));
            }
        }

        Logger.Information("Loaded {0} wizbangs.", 
            Logger.Args(s_templateManager.m_templates.Count));
    }

    /// <summary>
    /// Checks if a WizBang with the specified name exists.
    /// </summary>
    /// <param name="wizBangName">The name of the WizBang to check.</param>
    /// <returns><c>true</c> if a WizBang with the specified name exists; otherwise, <c>false</c>.</returns>
    internal static bool DoesWizBangExist(string wizBangName)
        => s_templateManager.m_templates.Any(x => x.m_name == wizBangName);

    public void DisposeStream() => base.Stream.Dispose();

}
