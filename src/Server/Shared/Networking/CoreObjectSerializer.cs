using WizUnraveler.Cache;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;

namespace Imlight.Server.Shared.Networking
{
    public class CoreObjectSerializer : ObjectSerializer
    {
        public CoreObjectSerializer()
        {
            base.SerializerMode = Mode.Compact;
            base.Options = SerializerFlags.UseFlags | SerializerFlags.ZLibCompress;
            base.PropertyMask = PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit;
        }

        protected override bool PreWriteObject(BitIterator writer, PropertyClass propClass)
        {
            if (propClass is null)
            {
                writer.WriteInt8(0);
                writer.WriteInt8(0);
                writer.WriteUInt32(0);
                return false;
            }
            
            var coreObjectData = GetCoreObjectData(propClass);

            writer.WriteUInt8(coreObjectData.Item1);  // class ID
            writer.WriteUInt8(coreObjectData.Item2);  // Namespace ID

            // Write the template ID if this is a CoreObject. Otherwise, write the hash.
            if (propClass is TypeCache.CoreObject co) 
                writer.WriteUInt32((uint)(co.m_templateID & 0xFFFFFFFF));
            else 
                writer.WriteUInt32(propClass.GetHash());

            return true;
        }

        protected override bool PreloadObject(BitIterator buffer, out PropertyClass propClass)
        {
            // First, read a temporary hash as if it were a PropertyClass.
            // If we find a property class from the hash, we know it's not a CoreObject.
            var startingPos = buffer.TellBitPos();
            var tempHash = buffer.ReadUInt32();
            if (tempHash == 0)
            {
                propClass = null;
                return false;
            }

            var tempProp = TypeCache.Dispatch(tempHash);
            if (tempProp is not null)
            {
                propClass = tempProp;

                return true;
            }

            buffer.SeekBit(startingPos);
            propClass = GetCoreObjectFromHeader(buffer);

            return propClass != null;
        }

        private static (byte, byte) GetCoreObjectData(PropertyClass propClass)
        {
            if (propClass is null) return (0, 0);

            return propClass.GetHash() switch
            {
                350837933 => // ClientObject
                    (2, 2),
                766500222 => // WizClientObject
                    (104, 2),
                1653772158 => // WizClientObjectItem
                    (115, 9),
                1167581154 => // WizClientPet
                    (106, 2),
                2109552587 => // WizClientMount
                    (108, 2),
                398229815 => // ClientReagentItem
                    (132, 9),
                958775582 => // ClientRecipe
                    (131, 131),
                _ => (0, 0)
            };
        }

        private static PropertyClass GetCoreObjectFromHeader(BitIterator buffer)
        {
            var classId = buffer.ReadUInt8();
            var namespaceId = buffer.ReadUInt8();
            var templateId = buffer.ReadUInt32();

            if (classId == 0 && namespaceId == 0)
            {
                return TypeCache.Dispatch(templateId);
            }

            return (classId, namespaceId, templateId) switch
            {
                (104, 2, 1) => // WizClientObject
                    new TypeCache.WizClientObject(),
                _ => null
            };
        }

    }
}
