using System.Xml.Linq;

namespace Seg
{
    public abstract class Verb
    {

        

        public int priority;

        public string name;

        public string verbId;

        //public bool isActive;

        
        internal void serialize(XElement xel)
        {


            xel.Add(new XAttribute("serId", verbId));
            //xel.Add(new XAttribute("isActive", isActive));



        }

        public abstract string translated_name(XDocument xdocObj);

        internal void deserialize(XElement el)
        {


            //isActive = bool.Parse(el.Attribute("isActive").Value);
        }

    }
}
