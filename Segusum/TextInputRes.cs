using System.Collections.Generic;

namespace Seg
{

        public class ExplanationWithCont : Explanation
        {
                public Explanation[] Continuations { get; set; }

                public ExplanationWithCont(string expId, string nameUntransl) : base(expId, nameUntransl)
                {
                        
                        //this.Continuations = conts;
                }

        }

        public class ExplanationWithContClient : ExplanationClient
        {
                public ExplanationClient[] eclContinuations { get; set; }

                public ExplanationWithContClient(string expId, string nameTransl, ExplanationClient[] conts) : base(expId, nameTransl)
                {

                        this.eclContinuations = conts;

                }

        }

        public class Explanation
        {
                public Explanation(string expId, string nameUntransl)
                {
                        this.expId = expId;
                        this.exName = nameUntransl;
                }

                public string expId { get; set; }

                public string exName { get; }

                public override string ToString()
                {
                        return exName;
                }

        }

        public class ExplanationClient
        {
                public ExplanationClient(string expId, string nameTransl)
                {
                        this.expId = expId;
                        this.exName = nameTransl;
                }

                public string expId { get; set; }

                public string exName { get; set; }


        }



}
