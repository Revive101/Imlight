/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Drawing;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using System.Threading.Tasks;
using static Imlight.Common.Caches.TypeCache;
using static Imlight.Common.Caches.TypeCache.CombatParticipant;
using Imlight.CoreLib.Game.Models.World;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Combat;

/// <summary>
/// Represents a sub circle in a duel. Each sub circle has a unique id, location, and yaw.
/// Each sub circle can be occupied by a player or creature.
/// </summary>
public class DuelActorSubCircle {
    private const float AggroTimeInSeconds = 0.75f;

    public DuelActor DuelActor { get; set; }
    public Vector3 Location { get; set; }
    public byte SubCircleId { get; set; }
    public float Yaw { get; set; } // Useless for now, but client records this for whatever reason
    public bool IsOccupied { get; set; }
    public Team Team { get; set; }
    public IActorRef ParticipantActor { get; private set; }
    public CoreObject ParticipantObject { get; private set; }
    public WizGameStats ParticipantGameStats { get; private set; }

    private readonly ulong _sigilId;

    public DuelActorSubCircle(DuelActor duelActor, Vector3 location, float yaw, ulong sigilId, byte subCircleId) {
        DuelActor = duelActor;
        Location = location;
        SubCircleId = subCircleId;
        Yaw = yaw;
        _sigilId = sigilId;
    }

    internal async Task AssignParticipant(IActorRef actor, CoreObject participantObject) {
        ParticipantActor = actor;
        ParticipantObject = participantObject;
        ParticipantGameStats = ((WizClientObject)participantObject).m_gameStats ?? new WizGameStats();
        Team = participantObject.m_templateID == 1 ? Team.Player : Team.Creature;
        IsOccupied = true;

        await PlayEntranceAnimation(participantObject);
    }

    internal CombatParticipant GetParticipant() {
        if (Team == Team.Player) {
            return GetPlayerParicipant();
        }
        else {
            return GetCreatureParticipant();
        }
    }

    private CombatParticipant GetPlayerParicipant() {
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var queryCharacterRsp = ParticipantActor
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        var combatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023255553, // Captued 2199023255553 from live
            m_isPlayer = true,
            m_teamID = 0,
            m_primaryMagicSchoolID = 83375795,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_originalTeam = 0,
            m_maxHandSize = 7,
            m_playerHealth = queryCharacterRsp.GameStats.m_currentHitpoints,
            m_maxPlayerHealth = queryCharacterRsp.GameStats.m_baseHitpoints,
            m_myTeamTurn = true,

            m_subcircle = 4,
            m_dynamicSymbol = DynamicSigilSymbol.Sun,
        };

        return combatParticipant;
    }

    private CombatParticipant GetCreatureParticipant() {
        var combatParticipant = new CombatParticipant {
            m_ownerID = ParticipantObject.m_globalID,
            m_templateID = 2199023290637, // Captured 2199023290637 from live
            m_isPlayer = false,
            m_isMonster = 1u,
            m_teamID = 1,
            m_originalTeam = 1,
            m_maxHandSize = 7,
            m_primaryMagicSchoolID = 83375795,
            m_pipCount = new() { m_powerPips = 0, m_genericPips = 1 },
            m_pipRoundRates = new(),
            m_playerHealth = 55,
            m_maxPlayerHealth = 55,

            m_subcircle = 0,
            m_dynamicSymbol = DynamicSigilSymbol.Dagger,
        };

        return combatParticipant;

    }

    private async Task PlayEntranceAnimation(CoreObject participantObject) {
        // Set the state of the participant to entering sigil.
        DuelActor.DuelBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = participantObject.m_globalID,
            State = (uint) NPCStates.Sigil
        });

        // Send aggro to the participant.
        DuelActor.DuelBroadcast(new WIZARD_12_PROTOCOL.MSG_AGGRO {
            GlobalID = participantObject.m_globalID,
            LocX = Location.X,
            LocY = Location.Y,
            LocZ = Location.Z,
            Yaw = Yaw,
            SigilGID = _sigilId
        });

        // Wait the amount of time it takes for the actor to enter the sigil, then set
        // their state to combat idle.
        await Task.Delay((int) (AggroTimeInSeconds * 1000));

        // Set state to stationary.
        DuelActor.DuelBroadcast(new GAME_5_PROTOCOL.MSG_ENTERSTATE() {
            GameObjectID = participantObject.m_globalID,
            State = (uint) NPCStates.Stationary
        });
    }
}
