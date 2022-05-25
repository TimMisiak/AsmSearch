using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AsmSearch
{
    internal class DataModelObject
    {
        XElement m_root;

        public DataModelObject(string xml)
        {
            m_root = XElement.Parse(xml).Element("Element") ?? throw new Exception("No root element in data model xml");
        }

        private DataModelObject(XElement root)
        {
            m_root = root;
        }

        public string? Name
        {
            get => m_root.Attribute("Name")?.Value;
        }

        public IEnumerable<DataModelObject> IteratedChildren => m_root.Elements().Where(x => x.Attribute("Iterated")?.Value == "true").Select(x => new DataModelObject(x));

        public DataModelObject this[string name]
        {
            get
            {
                return new DataModelObject(m_root.Elements().Single(x => x.Attribute("Name")?.Value == name));
            }
        }

        public ulong ValueAsInt
        {
            get
            {
                var editValue = m_root.Attribute("EditValue")?.Value ?? throw new Exception("No EditValue where expected in data model xml");
                if (!editValue.StartsWith("0x"))
                {
                    throw new Exception($"Expecting hex integer but found '{editValue}' in data model xml");
                }
                return ulong.Parse(editValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
            }
        }


        public string DisplayValue => m_root.Attribute("DisplayValue")?.Value ?? throw new Exception("No DisplayValue where expected in data model xml");
    }
}
