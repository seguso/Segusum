using System.Collections.Generic;

namespace Seg
{
    public class ObjectiveClient
    {
                public string ser_id;
                public string readable_name;


                public bool obcWasSeen { get; set; }
                //public bool oc_showsUse { get; set; }

                //public List<string> oc_verbsToShow;

                //public bool oc_requiresBecause { get; set; }

                public string obcContainedSubject { get; set; }

                public bool obcDoNotShowExplanations { get; set; }


                public ExplanationClient[] obcCustomExplanations { get; set; }

                public string obcCustomExplanationIntro { get; set; }
                public string obcCustomExplanationFailureTemplate{ get; set; }

                //public string[] ocAssociatedQtokens { get; set; }

                //public string[] ocExcludedQtokens { get; set; }

                public ObjectiveClient(string ser_id, string readable_name, bool wasSeen/*List<string> oc_verbsToShow, bool requiresBecause, string[] associatedQtoks*//*, bool showsUse *//*, string[] excludedQtoks*/, bool obcDoNotShowExplanations,  string containedSubject, string customExplanationsIntro = null, ExplanationClient[] customExplanations = null, string customExplanationsFailureTemplate = null)
                {
                        this.obcContainedSubject =  containedSubject;
                        this.ser_id = ser_id;
                        this.readable_name = readable_name;
                        this.obcWasSeen = wasSeen;
                        this.obcDoNotShowExplanations = obcDoNotShowExplanations;

                        this.obcCustomExplanationIntro = customExplanationsIntro;
                        this.obcCustomExplanationFailureTemplate = customExplanationsFailureTemplate;
                        this.obcCustomExplanations = customExplanations;
                        //this.oc_verbsToShow = oc_verbsToShow;
                        //   oc_requiresBecause = requiresBecause;
                        //this.ocAssociatedQtokens = associatedQtoks;
                        //ocExcludedQtokens = excludedQtoks;
                        //oc_showsUse = showsUse;
                }
        }
}
