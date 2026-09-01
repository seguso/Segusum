using System;

namespace Seg
{

        public class PastActionAskForHint : PastAction
        {
                public Objective pu;
        }
        //public class PastActionUseInLocation: PastAction
        //{

        //        public BinVerb binVerb;
        //        public LogicObj lo;
        //        public Room ro;
        //        public Objective pu;


        //        //public override bool contains_obj(LogicObj o)
        //        //{
        //        //        return (lo == o);
        //        //}
        //}


        public class PastActionPickup: PastAction
        {

                public LogicObj lo;
        }

        public class PastActionUseHere : PastAction
        {

                public LogicObj lo;

                public PastActionUseHere(LogicObj lo, string fullText, DateTime date)
                {
                        this.lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        this.fullText = fullText ?? throw new ArgumentNullException(nameof(fullText));
                        this.dateTime = date;
                }

                public string fullText { get; set; }
        }

        public class PastActionLookRemember: PastAction
        {

                public LogicObj lo;

                public PastActionLookRemember(LogicObj lo, string fullText, DateTime date)
                {
                        this.lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        this.fullText = fullText ?? throw new ArgumentNullException(nameof(fullText));
                        this.dateTime = date;
                }

                public string fullText { get; set; }
        }

}
