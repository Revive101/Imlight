/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Text;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Newtonsoft.Json;

namespace Imlight.CoreLib.Shared.Behaviors;

[Serializable]
public class ServerWizPlayerNameBehavior : IClientBehaviorProvider<ClientWizPlayerNameBehavior> {

    private const string FemaleSourcePrefix = "80";
    private const string MaleSourcePrefix = "82";

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

    public string GetWizardNameAsByteHexString() {
        // Drop the MSB from input, then convert it to a hex string.
        var raw = (NameIndices & 0x7FFFFFFF).ToString("X8");
        var sb = new StringBuilder(raw);
        for (int i = sb.Length - 2; i >= 0; i -= 2) {
            sb.Insert(i, ' ');
        }

        var tail = sb.ToString().TrimStart();

        // Replace the first 2 characters depending on gender.
        var newMsb = Gender == eGender.Female ? FemaleSourcePrefix : MaleSourcePrefix;
        tail = newMsb + tail[2..];

        return tail;
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
