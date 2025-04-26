/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizPlayerNameBehavior : IClientBehaviorProvider<ClientWizPlayerNameBehavior> {

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

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
        // If the name override is set, we want to add the friendly player icon to the name
        // since the client won't do it automatically anymore.
        var nameOverride = new WideByteString();
        if (NameOverride.Length > 0 && FriendlyPlayer) {
            nameOverride = $"<image;FriendlyPlayer> {NameOverride} <image;FriendlyPlayer>";
        }
        else {
            nameOverride = NameOverride;
        }

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
            m_wsNameOverride = nameOverride
        };
    }
    
}
