using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
    public class exit
    {
        public Room From { get; set; }
        public Room To { get; set; }


        public void serialize(XElement xel)
        {
            xel.Add(new XAttribute("from", From.roomId));
            xel.Add(new XAttribute("to", To.roomId));
        }
    }
}
