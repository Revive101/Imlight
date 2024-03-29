/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Combat;

internal class CombatAIActor : ReceiveProtocolDispatcher {
    private readonly IActorRef _creatureActorRef;
    private readonly CombatDuelActor _duelActor;
    private readonly CombatDuelActorSubCircle _mySubcircle;
    private readonly WizGameStats _stats;
    private readonly float _intelligenceFactor;
    private readonly float _selfishnessFactor;
    private readonly float _aggressivenessFactor;
    private readonly MagicSchool _magicSchool;
    private readonly int _level;

    // ctor
    public CombatAIActor(IActorRef creatureActor, CombatDuelActor duelActor, CombatDuelActorSubCircle mySubcircle) {
        this._creatureActorRef = creatureActor;
        this._duelActor = duelActor;
        this._mySubcircle = mySubcircle;

        // Query the creature actor for the creature's stats
        var rsp = _creatureActorRef
            .Ask<COMBAT_106_PROTOCOL.MSG_CREATURESTATS>(new COMBAT_106_PROTOCOL.MSG_QUERYCREATURESTATS())
            .Result;
        this._stats = rsp.GameStats;
        this._intelligenceFactor = rsp.CombatIntelligence;
        this._selfishnessFactor = rsp.CombatSelfishFactor;
        this._aggressivenessFactor = rsp.CombatAggressionFactor;
        this._magicSchool = rsp.MagicSchool;
        this._level = rsp.CombatLevel;
    }

    // Akka.NET ctor
    public static Props Props(IActorRef creatureActor, CombatDuelActor duelActor, CombatDuelActorSubCircle mySubcircle)
        => Akka.Actor.Props.Create(() => new CombatAIActor(creatureActor, duelActor, mySubcircle));

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_NEWROUND))]
    private void ReceiveNewCombatRound(COMBAT_106_PROTOCOL.MSG_NEWROUND message) {
        var hand = _mySubcircle.DrawHand();
    }
}
