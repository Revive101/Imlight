/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Xml;

namespace CacheGenerator;

public class ProtocolHeader {
    private const string WizardCombatProtocolType = "WIZARDCOMBAT";
    private const string DuplicateProtocolType = "MG9";

    public byte ServiceId { get; init; }
    public string? ProtocolType { get; init; }
    public int ProtocolVersion { get; init; }
    public string? ProtocolDescription { get; init; }

    public ProtocolHeader(XmlDocument xmlDocument) {
        if (xmlDocument is null) {
            throw new NullReferenceException(nameof(xmlDocument));
        }

        var serviceId = Convert.ToByte(xmlDocument.DocumentElement?.SelectSingleNode("//_ProtocolInfo/RECORD/ServiceID")?.InnerText);
        var protocolType = xmlDocument.DocumentElement?.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolType")?.InnerText;
        var protocolVersion = Convert.ToInt32(xmlDocument.DocumentElement?.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolVersion")?.InnerText);
        var protocolDescription = xmlDocument.DocumentElement?.SelectSingleNode("//_ProtocolInfo/RECORD/ProtocolDescription")?.InnerText;

        // Check to see if any of the header properties are null. If so, throw exception.
        if (serviceId is 0 || protocolType is null || protocolVersion is 0 || protocolDescription is null) {
            throw new NullReferenceException(nameof(serviceId));
        }

        // The original developers named the WizardCombat protocol incorrectly. This is a failsafe to ensure that the
        // protocol is named correctly.
        if (serviceId == 51) {
            protocolType = WizardCombatProtocolType;
        }

        // Another fallback. Service ID 54 is a duplicate.
        if (serviceId == 54) {
            protocolType = DuplicateProtocolType;
        }

        // Set the properties.
        this.ServiceId = serviceId;
        this.ProtocolType = protocolType.ToUpper();
        this.ProtocolVersion = protocolVersion;
        this.ProtocolDescription = protocolDescription;
    }
}
