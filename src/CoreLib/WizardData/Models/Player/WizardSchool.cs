/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.WizardData.Models.Player;

public enum WizardSchoolEnum {
    Fire = 2343174,
    Ice = 72777,
    Storm = 83375795,
    Life = 2330892,
    Myth = 2448141,
    Death = 78318724,
    Balance = 1027491821,
}

public class WizardSchool {
    public byte Level { get; set; }
    public int TrainingPoints { get; set; }
    public int XpToNextLevel { get; set; }
}
