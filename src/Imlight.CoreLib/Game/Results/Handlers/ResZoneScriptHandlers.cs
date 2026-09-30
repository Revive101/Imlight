using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// Hands a token, counter or puzzle-variable result to the zone, which keeps it in its script state (per zone
/// instance). The zone applies messages in order, so a later ResPostEvent sees the change.
/// </summary>
internal static class ZoneScriptResult {

    public static bool Send(IResultContext context, Result result) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null || result is null) {
            return false;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONESCRIPTRESULT {
            Result = result,
            PlayerGameObject = context.GetPlayerObj(),
        }, ActorRefs.NoSender);

        return true;
    }

}

/// <summary>
/// ResZoneTokenEnable: turns a zone token on.
/// </summary>
internal sealed class ResZoneTokenEnableHandler : BaseResultHandler<ResZoneTokenEnable> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}

/// <summary>
/// ResZoneTokenDisable: turns a zone token off.
/// </summary>
internal sealed class ResZoneTokenDisableHandler : BaseResultHandler<ResZoneTokenDisable> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}

/// <summary>
/// ResZoneTokenModify: adds to a zone token's value.
/// </summary>
internal sealed class ResZoneTokenModifyHandler : BaseResultHandler<ResZoneTokenModify> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}

/// <summary>
/// ResZoneTokenReset: resets a zone token.
/// </summary>
internal sealed class ResZoneTokenResetHandler : BaseResultHandler<ResZoneTokenReset> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}

/// <summary>
/// ResZoneCounter: changes a zone counter.
/// </summary>
internal sealed class ResZoneCounterHandler : BaseResultHandler<ResZoneCounter> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}

/// <summary>
/// ResEncounterSetVariable: sets a puzzle variable.
/// </summary>
internal sealed class ResEncounterSetVariableHandler : BaseResultHandler<ResEncounterSetVariable> {

    public override bool Execute(IResultContext context) => ZoneScriptResult.Send(context, Result);

}
