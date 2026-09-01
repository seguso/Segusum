using System.Linq;
using System.Xml.Linq;

namespace Seg
{
    public class ZeroVerb: Verb
    {
        /// <summary>
        ///  talk is a special zero verb that triggers the same cutscenes that are triggered when you change room
        /// </summary>
        public bool is_talk;

        public bool is_ask_for_hint = false;

        public override string translated_name(XDocument xdocObj)
        {
            if (xdocObj== null)
            {
                return this.name;
            }
            string nameTransl;


            //var xmlPath = WorldBase.getPathXmlTranslationObjs(lang);
            //var xdoc = XDocument.Load(xmlPath);
            var el = xdocObj.Root.Elements("zero_verb").Where(lel => lel.Attribute("verb_id").Value == this.verbId).FirstOrDefault();
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
    }
}
