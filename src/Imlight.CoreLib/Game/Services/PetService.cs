/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;

namespace Imlight.CoreLib.Game.Services;

internal class PetService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {
    private static readonly int s_petEnergyTickIntervalInSeconds = ConfigurationManager.Settings.PetEnergyTickInSeconds;
    private const int PET_ENERGY_TICK_DELAY = 2;

    public ITimerScheduler Timers { get; set; }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PetService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        // Inform the client of the pet's current energy after login.
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;

        // The client has a max energy increase effect applied, so sending it here would double the energy client side.
        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        // If the last energy tick has passed, the tick time will be now + the tick interval.
        // Otherwise, the tick time will be the last tick time normally.
        var tickTime = petOwnerBehavior.LastEnergyTickEpoch;
        if (tickTime <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
            tickTime = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s_petEnergyTickIntervalInSeconds);
        }

        var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = wizard.CharId,
            Energy = petOwnerBehavior.Energy,
            MaxEnergy = normMaxEnergy,
            TickTime = (int) tickTime
        };

        SendToSocket(tickMsg);

        // The game client has a small delay between updating the energy and energy timer.
        // This delay is to ensure the client's energy timer is in sync with actual energy gain.
        // Convert the tick time to seconds.
        var tickTimeSeconds = tickTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var period = TimeSpan.FromSeconds(tickTimeSeconds + PET_ENERGY_TICK_DELAY);
        Timers.StartPeriodicTimer("petEnergyTick", new CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK(), period, period);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK))]
    private void ReceiveDoEnergyTick(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK message) {
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;

        // The client has a max energy increase effect applied, so sending it here would double the energy client side.
        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var tickTime = (int) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s_petEnergyTickIntervalInSeconds);

        if (petOwnerBehavior.Energy < normMaxEnergy) {
            wizard.UpdateEnergy(petOwnerBehavior.Energy + 1);
            var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
                GlobalID = wizard.CharId,
                Energy = petOwnerBehavior.Energy,
                MaxEnergy = normMaxEnergy,
                TickTime = tickTime
            };

            SendToSocket(tickMsg);
        }
    }
}