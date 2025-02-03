/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Behaviors;
using Newtonsoft.Json;

namespace Imlight.CoreLib.WizardData.Models.Misc;

public sealed class ScoreTracking : IClientBehaviorProvider<TypeCache.ScoreTracking> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public string MinigameName { get; set; }
    public float GameScore { get; set; }
    public GID GamerId { get; set; }
    public uint GamerNameIndices { get; set; }
    public WideByteString GamerNameOverride { get; set; }
    public ByteString GamerGender { get; set; }

    public TypeCache.ScoreTracking GetClientBehaviorInstance() 
        => new() {
            m_gamerID = GamerId,
            m_gamerName = GamerNameOverride,
            m_gamerNameCode = GamerNameIndices,
            m_gameScore = GameScore,
            m_gamerGender = GamerGender,
            m_gamerRace = "Human"
        };
}