/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.Game.Combat;

internal enum CombatTeam {
    Player = 0,
    Monster = 1,
}

public enum CombatMoveType {
    Attack = 0,
    Flee = 1,
    Discard = 2,
    Pass = 3,
    ChangeMind = 4,
}