using System.Collections.Concurrent;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Requirement classes the server does not track (effects, transformations, the tutorial, placeholders whose
/// purpose is unknown). They passed while their class decoded as null, so they keep passing; each class logs
/// once so a trigger that depends on one can be found.
/// </summary>
internal static class UntrackedRequirement {

    private static readonly ConcurrentDictionary<string, byte> s_logged = new();

    public static bool Pass(string className) {
        if (s_logged.TryAdd(className, 0)) {
            Logger.Information("Requirement {0} is not tracked by the server; it always passes.", Logger.Args(className));
        }

        return true;
    }

}

internal sealed class ReqUnknown0FF838ACHandler : BaseRequirementHandler<ReqUnknown0FF838AC> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown0FF838AC));

}

internal sealed class ReqUnknown14CA64DEHandler : BaseRequirementHandler<ReqUnknown14CA64DE> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown14CA64DE));

}

internal sealed class ReqTutorialStageHandler : BaseRequirementHandler<ReqTutorialStage> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqTutorialStage));

}

internal sealed class ReqHasEffectHandler : BaseRequirementHandler<ReqHasEffect> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqHasEffect));

}

internal sealed class ReqHasPolymorphEffectHandler : BaseRequirementHandler<ReqHasPolymorphEffect> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqHasPolymorphEffect));

}

internal sealed class ReqUnknown50E82908Handler : BaseRequirementHandler<ReqUnknown50E82908> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown50E82908));

}

internal sealed class ReqUnknown02D1D615Handler : BaseRequirementHandler<ReqUnknown02D1D615> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown02D1D615));

}

internal sealed class ReqUnknown2E532A74Handler : BaseRequirementHandler<ReqUnknown2E532A74> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown2E532A74));

}

internal sealed class ReqHasTransformationHandler : BaseRequirementHandler<ReqHasTransformation> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqHasTransformation));

}

internal sealed class ReqUnknown2C948ACCHandler : BaseRequirementHandler<ReqUnknown2C948ACC> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown2C948ACC));

}

internal sealed class ReqUnknown3569555FHandler : BaseRequirementHandler<ReqUnknown3569555F> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown3569555F));

}

internal sealed class ReqUnknown6A164DC9Handler : BaseRequirementHandler<ReqUnknown6A164DC9> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown6A164DC9));

}

internal sealed class ReqUnknown001CD4F2Handler : BaseRequirementHandler<ReqUnknown001CD4F2> {

    public override bool Evaluate(IRequirementContext context) => UntrackedRequirement.Pass(nameof(ReqUnknown001CD4F2));

}
