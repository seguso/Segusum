using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
    public class BinVerb : Verb
    {


        public bool requiresPuzzle = true;

        public bool canBeUnaryOrBinaryDependingOnObj = false;

        public string secondPart;

        public string firstPart;

        public override string ToString()
        {
            return name;
        }

        public bool charIsAlwaysLast = false;

        public bool charIsAlwaysFirst = false;

        public override string translated_name(XDocument xdocObj)
        {
            if (xdocObj== null)
            {
                return name;

            }
            string nameTransl;


            //var xmlPath = WorldBase.getPathXmlTranslationObjs(lang);
            //var xdoc = XDocument.Load(xmlPath);
            var el = xdocObj.Root.Elements("bin_verb").Where(lel => lel.Attribute("verb_id").Value == this.verbId).FirstOrDefault();
            if (el != null && el.Attribute("transl").Value != "+")
            {
                nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

            }
            else
            {
                nameTransl = name;
            }

            return nameTransl;
        }


        public string translated_second_part(XDocument xdocObj)
        {
            if (xdocObj == null)
            {
                return this.secondPart;
            }
            string nameTransl;


            //var xmlPath = WorldBase.getPathXmlTranslationObjs(lang);
            //var xdoc = XDocument.Load(xmlPath);
            var el = xdocObj.Root.Elements("bin_verb_second_part").Where(lel => lel.Attribute("verb_id").Value == this.verbId).FirstOrDefault();
            if (el != null && el.Attribute("transl").Value != "+")
            {
                nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

            }
            else
            {
                nameTransl = this.secondPart;
            }

            return nameTransl;
        }

    }
}
