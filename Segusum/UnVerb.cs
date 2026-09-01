using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
    public class UnVerb : Verb
    {

        public override string translated_name(XDocument xdocObj)
        {
            if (xdocObj == null)
            {
                return this.name;
            }
            string nameTransl;

            
            //var xmlPath = WorldBase.getPathXmlTranslationObjs(lang);
            //var xdoc = XDocument.Load(xmlPath);
            var el = xdocObj.Root.Elements("un_verb").Where(lel => lel.Attribute("verb_id").Value == this.verbId).FirstOrDefault();
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

        public bool requires_objective = true;

        public bool is_remember = false;
                public bool isPickup= false;


                public bool canOnlyBeUsedWithRoomObjectsNotInv;




    }
}
