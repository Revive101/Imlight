using Imlight.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Imlight.Engine.DML
{
    public class DMLElement
    {

        public enum DataType
        {
            BYT,
            UBYT,
            SHRT,
            USHRT,
            INT,
            UINT,
            STR,
            WSTR,
            FLT,
            DBL,
            GID,
            UNKNOWN
        }

        public string Name { get; private set;  }
        public DataType Type { get; private set; }
        public bool NoTransfer { get; private set; }
        public object Value { get; set; }

        /// <summary>
        /// Attempts to set this object's data from the XML record element.
        /// </summary>
        /// <param name="element">The original XML element.</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool FromXml(XmlElement element)
        {
            this.Name = element.Name;

            // Try to get the data type.
            string dataTypestring = GetDataTypeString(element);
            this.Type = GetDataTypeFromString(dataTypestring);

            // Set the NOXFER attribute, if it exists.
            // Anytime this attribute exists, it is true.
            string noTransfer = element.GetAttribute("NOXFER");
            if (noTransfer != "" && noTransfer is not null)
                this.NoTransfer = true;

            // If the element starts with '_', it's a metadata node.
            // The value exists in the XML, as it's not a part of the serialization process.
            if (Name.StartsWith('_')) this.Value = element.InnerText;
            else this.Value = null;

            return true;
        }

        public bool IsMetadata() => this.NoTransfer;

        private DataType GetDataTypeFromString(string dataTypeString)
        {
            if (Enum.TryParse(typeof(DataType), dataTypeString, true, out var result))
            {
                return (DataType)result;
            }
            else
            {
                // Kingisle accidently spells "UBYT" as a "UBYTE" exactly one time.
                // If that happens, read it as it's proper spelling.
                if (dataTypeString == "UBYTE")
                {
                    Enum.TryParse(typeof(DataType), "UBYT", true, out var failsafeResult);
                    return (DataType)failsafeResult;
                }

                Log.Error($"Could not parse data type \"{dataTypeString}\"!");
                return DataType.UNKNOWN;
            }
        }

        private string GetDataTypeString(XmlElement element)
        {
            string dataTypeString = element.GetAttribute("TYPE");

            // Unfortunately this has to exist because the developers are inconsistent.
            // Failsafe conditions:
            if (dataTypeString == "")
            {
                string mistypedDataTypeString = element.GetAttribute("TPYE");
                if (mistypedDataTypeString == "")
                {
                    if (element.Name == "GlobalID") dataTypeString = "GID";
                }
                else dataTypeString = mistypedDataTypeString;
            }

            return dataTypeString;
        }

    }
}
