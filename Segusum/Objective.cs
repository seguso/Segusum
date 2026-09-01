using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{
        /// <summary>
        /// a puzzle is a task that the character knows he needs to do, but it is not obvious HOW to do it.
        /// </summary>
        public class Objective : Mentionable
        {

                // non da serializzare
                public string serId;
                public string nameReadable;


                public string ContainedSubject { get; set; }

                public string CustomExplanationsIntro { get; set; }

                public Explanation[] CustomExplanations { get; set; }

                public string CustomExplanationsFailureTemplate { get; set; }

                //public List<Qtok> associatedQToks = new List<Qtok> { };


                //internal List<Qtok> excludedQtoks = new List<Qtok> { };




                //public Func<bool> you_have_all_data_to_solve_it;
                //public bool has_at_least_a_clue_in_past_scenes;


                // da serializzare
                public int how_many_times_tried = 0;

                public int howManyTimesSeen = 0;

                public DateTime? objFirstTimeSeen;

                // Semantic timestamp of the first successful solution.  It is
                // nullable because old saves can prove that an objective was
                // solved without preserving the moment when that happened.
                public DateTime? SolvedAt;


                public override string ToString()
                {
                        return nameReadable;
                }

                public string translated_name(WorldBase w, XDocIndexed xdocObj)
                {
                        if (xdocObj == null)
                        {
                                return nameReadable;
                        }

                        var tra1 = w.translateDialogOrNarOrAnnotatedAux(nameReadable, xdocObj, out bool? found);

                        if (found != false)
                        {
                                return tra1;
                        }
                        else //if (found == false)
                        {
                                // fallback al vecchio file
                                string nameTransl;


                                //var xmlPath = WorldBase.getPathXmlTranslationObjs(lang);
                                //var xdoc = XDocument.Load(xmlPath);
                                if (xdocObj.objectiveOfSerId.ContainsKey(serId))
                                {
                                        var el = xdocObj.objectiveOfSerId[serId]; // Root.Elements("objective").Where(lel => lel.Attribute("ser_id").Value == this.serId).FirstOrDefault();
                                        if (el != null && el.Attribute("transl").Value != "+")
                                        {
                                                nameTransl = el.Attribute("transl").Value.Replace("''", "\"");

                                        }
                                        else
                                        {
                                                nameTransl = nameReadable;
                                        }
                                }
                                else
                                {
                                        nameTransl = nameReadable;
                                }

                                return nameTransl;
                        }
                }

                //public bool must_be_disabled_now()
                //{
                //    if (you_have_all_data_to_solve_it == null ||   !you_have_all_data_to_solve_it()) {
                //        return false;

                //    }
                //    else
                //    {
                //        return how_many_times_tried >= 2;
                //    }
                //}

                public XElement serialize(XElement parent)
                {
                        var xel = new XElement("objective");
                        xel.Add(new XAttribute("how_many_times_tried", how_many_times_tried));
                        xel.Add(new XAttribute("howManyTimesSeen", howManyTimesSeen));
                        if (objFirstTimeSeen != null)
                        {
                                xel.Add(new XAttribute("objFirstTimeSeen", objFirstTimeSeen.Value.ToString(CultureInfo.InvariantCulture)));
                        }
                        if (SolvedAt != null)
                        {
                                xel.Add(new XAttribute("solvedAt", SolvedAt.Value.ToString(CultureInfo.InvariantCulture)));
                        }
                        xel.Add(new XAttribute("ser_id", serId));
                        parent.Add(xel);

                        return xel;

                }

                public void deserialize(XElement xel)
                {
                        how_many_times_tried = int.Parse(xel.Attribute("how_many_times_tried").Value);

                        XAttribute xatSeen = xel.Attribute("howManyTimesSeen");
                        if (xatSeen != null)
                        {
                                howManyTimesSeen = int.Parse(xatSeen.Value);
                        }
                        else
                        {
                                howManyTimesSeen = 0;
                        }



                        XAttribute xatftseen = xel.Attribute("objFirstTimeSeen");
                        if (xatftseen != null)
                        {
                                objFirstTimeSeen = DateTime.Parse(xatftseen.Value, CultureInfo.InvariantCulture);
                        }
                        else
                        {
                                objFirstTimeSeen = null;
                        }

                        XAttribute xatSolvedAt = xel.Attribute("solvedAt");
                        SolvedAt = xatSolvedAt == null
                                ? null
                                : DateTime.Parse(xatSolvedAt.Value, CultureInfo.InvariantCulture);
                }

                public bool IsSeen()
                {
                        return howManyTimesSeen > 0;
                }
        }
}
