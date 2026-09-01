using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
        public class XDocIndexed
        {
                public XDocument Xdoc { get; }
                public Dictionary<string, XElement> qtokOfSerId;

                public Dictionary<string, XElement> loOfLoId;

                public Dictionary<string, XElement> inTheHandOf_ofLoId;

                public Dictionary<string, XElement> charNameForDialogOfLoId;

                public Dictionary<string, XElement> objectiveOfSerId;

                public Dictionary<string, XElement> roomOfRoomId;
                public Dictionary<string, XElement> roomEnterOfRoomId;


                public Dictionary<string, string> GenericTransl { get; set; } = new Dictionary<string, string>();

                //public Dictionary<string, XElement> roomEnterAloneOfRoomId;
                //public Dictionary<string, XElement> roomEnterFollowedMaleOfRoomId;

                //public Dictionary<string, XElement> roomEnterFollowedFemaleOfRoomId;


                public string translate(string s, out bool found)
                {
                        if (GenericTransl.ContainsKey(s))
                        {
                                found = true;
                                return GenericTransl[s];
                        }
                        else
                        {
                                found = false;
                                return s;
                        }
                }

                public XDocIndexed(XDocument xdoc, XDocument xdocGeneric)
                {
                        this.Xdoc = xdoc;

                        qtokOfSerId = xdoc.Root.Elements("qtok").ToDictionary(x => x.Attribute("ser_id").Value);

                        loOfLoId = xdoc.Root.Elements("logic_obj").ToDictionary(x => x.Attribute("lo_id").Value);
                        charNameForDialogOfLoId = xdoc.Root.Elements("char_name_for_dialog").ToDictionary(x => x.Attribute("lo_id").Value);

                        inTheHandOf_ofLoId = xdoc.Root.Elements("logic_obj_in_the_hand_of").ToDictionary(x => x.Attribute("lo_id").Value);

                        objectiveOfSerId = xdoc.Root.Elements("objective").ToDictionary(x => x.Attribute("ser_id").Value);


                        roomOfRoomId = xdoc.Root.Elements("room").ToDictionary(x => x.Attribute("room_id").Value);
                        roomEnterOfRoomId= xdoc.Root.Elements("room_enter").ToDictionary(x => x.Attribute("room_id").Value);

                        foreach(var xel in xdocGeneric.Root.Elements("str"))
                        {
                                var orig = xel.Attribute("orig").Value;
                                var tra = xel.Attribute("transl").Value;

                                if (tra.Trim() != "+")
                                {

                                        if (!GenericTransl.ContainsKey(orig))
                                        {
                                                GenericTransl.Add(orig, tra);
                                        }
                                }
                                //try
                                //{
                                //        GenericTransl.Add(orig, tra);
                                //}
                                //catch
                                //{

                                //      //  throw new Exception($"stringa duplicata nel file di traduzioni: {orig}");
                                //}
                        }

                        //roomEnterAloneOfRoomId = xdoc.Root.Elements("room_enter_alone").ToDictionary(x => x.Attribute("room_id").Value);
                        //roomEnterFollowedMaleOfRoomId = xdoc.Root.Elements("room_enter_followed_male").ToDictionary(x => x.Attribute("room_id").Value);
                        //roomEnterFollowedFemaleOfRoomId = xdoc.Root.Elements("room_enter_followed_female").ToDictionary(x => x.Attribute("room_id").Value);
                }
        }
}
