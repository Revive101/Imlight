using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Imlight.Common.Logger;

namespace Imlight.Engine.DML
{
    public class DMLRecord : IDisposable, ICloneable
    {

        public List<DMLElement> Elements { get; }

        private bool _recordTemplateSet;

        // ctor
        public DMLRecord()
        {
            Elements = new List<DMLElement>();
            this._recordTemplateSet = false;
        }

        /// <summary>
        /// Attempts to set this object's template data from the XML record node.
        /// </summary>
        /// <param name="recordNode">The original XML record.</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool FromXml(XmlNode recordNode)
        {
            var xmlElements = recordNode.ChildNodes.OfType<XmlElement>();

            if (xmlElements.Count() <= 0) return false;

            foreach (var element in xmlElements)
            {
                // Create a new DMLElement object and attempt to set it's data.
                // On success, add it to the element list of this object.
                // Return the result of the operation either way.

                DMLElement dmlElement = new DMLElement();
                bool result = dmlElement.FromXml(element);

                if (result) Elements.Add(dmlElement);
                else return false;
            }

            this._recordTemplateSet = true;
            return true;
        }

        /// <summary>
        /// Attempts to set this object's data from a byte array. This method should be called *after* FromXml, as it requires those nodes written prior.
        /// </summary>
        /// <param name="rawBytes">The array of raw bytes, usually from a packet.</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool FromBinary(byte[] rawBytes)
        {
            if (rawBytes == null) throw new ArgumentNullException(nameof(rawBytes));
            if (!this._recordTemplateSet) 
                throw new Exception($"This DML record does not have it's template yet! Use {nameof(FromXml)} prior to calling this method.");

            // Create BinaryReader
            Stream stream = new MemoryStream(rawBytes);
            BinaryReader reader = new BinaryReader(stream);

            foreach (DMLElement element in this.Elements)
            {
                if (element.IsMetadata()) continue;
                if (element.Value != null) continue;

                switch (element.Type)
                {
                    case DMLElement.DataType.BYT:
                        element.Value = reader.ReadSByte();
                        break;
                    case DMLElement.DataType.UBYT:
                        element.Value = reader.ReadByte();
                        break;
                    case DMLElement.DataType.SHRT:
                        element.Value = reader.ReadInt16();
                        break;
                    case DMLElement.DataType.USHRT:
                        element.Value = reader.ReadUInt16();
                        break;
                    case DMLElement.DataType.INT:
                        element.Value = reader.ReadInt32();
                        break;
                    case DMLElement.DataType.UINT:
                        element.Value = reader.ReadUInt32();
                        break;
                    case DMLElement.DataType.WSTR:
                    case DMLElement.DataType.STR:
                        element.Value = reader.ReadString();
                        break;
                    case DMLElement.DataType.FLT:
                        element.Value = reader.ReadSingle();
                        break;
                    case DMLElement.DataType.DBL:
                        element.Value = reader.ReadDouble();
                        break;
                    case DMLElement.DataType.GID:
                        element.Value = reader.ReadUInt64();
                        break;
                    case DMLElement.DataType.UNKNOWN:
                        break;
                    default:
                        throw new Exception($"Could not deserialize type [{element.Type}] from binary!");
                }
            }

            return true;
        }

        // dtor
        ~DMLRecord()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose();
        }

        public void Dispose()
        {
            Elements.Clear();
            GC.SuppressFinalize(this);
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
