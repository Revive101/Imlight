/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.IO;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using System;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Serializable]
public class ServerWizPlayerNameBehavior : BehaviorInstance, IClientBehaviorProvider<ClientWizPlayerNameBehavior> {
    public uint NameIndices;
    public bool UseRank;
    public eGender Gender;
    public eRace Race;
    public ByteString BadgeTitle;
    public uint ChatPermissions;
    public uint PvpIconId;
    public uint LocaleId;
    public bool FriendlyPlayer;
    public bool Volunteer;
    public uint GuildName;
    public WideByteString NameOverride;

    public string GetWizardName() {
        if (NameOverride.Length > 0) {
            return NameOverride.ToString();
        }

        var actualName = WizardNameBank.GetEnglishName(NameIndices, Gender);
        return actualName;
    }

    public ClientWizPlayerNameBehavior GetClientBehaviorInstance() {
        return new ClientWizPlayerNameBehavior {
            m_nameKeys = NameIndices,
            m_useRank = UseRank,
            m_eGender = Gender,
            m_eRace = Race,
            m_badgeTitle = BadgeTitle,
            m_chatPermissions = ChatPermissions,
            m_pvpIconID = PvpIconId,
            m_localeID = LocaleId,
            m_friendlyPlayer = FriendlyPlayer,
            m_volunteer = Volunteer,
            m_guildName = GuildName,
            m_wsNameOverride = NameOverride
        };
    }
}
