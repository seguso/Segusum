using System;

namespace Seg
{
        public class PastActionQuat: PastAction
    {

        public BinVerb binVerb;
        public LogicObj lo1;
        public LogicObj lo2;
        public Objective puzzle;

        //public override bool contains_obj(LogicObj o)
        //{
        //    return (lo1 == o || lo2 == o);
        //}
    }


    public class PastActionBinNoOb: PastAction
    {

        public BinVerb binVerb;
        public LogicObj lo1;
        public LogicObj lo2;
        

        //public override bool contains_obj(LogicObj o)
        //{
        //    return (lo1 == o || lo2 == o);
        //}
    }

        public class PastActionUseWith: PastAction
        {

                public LogicObj lo1;
                public LogicObj lo2;
                public Explanation exp;

                public bool? handlerCalled;

                public PastActionUseWith(bool? handlerCalled, LogicObj lo1, LogicObj lo2, Explanation exp, string fullText, DateTime date)
                {
                        this.lo1 = lo1 ?? throw new ArgumentNullException(nameof(lo1));
                        this.lo2 = lo2 ?? throw new ArgumentNullException(nameof(lo2));
                        FullText = fullText ?? throw new ArgumentNullException(nameof(fullText));

                        this.handlerCalled = handlerCalled ;
                        this.dateTime = date;
                        this.exp = exp;
                }

                public string FullText { get; set; }


                //public override bool contains_obj(LogicObj o)
                //{
                //    return (lo1 == o || lo2 == o);
                //}
        }

        public class PastActionUseFor: PastAction
        {

                public LogicObj lo;
                public Objective ob;
                public Explanation exp;

                public PastActionUseFor(LogicObj lo, Objective ob, Explanation exp, DateTime date)
                {
                        this.lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        this.ob = ob ?? throw new ArgumentNullException(nameof(ob));
                        this.exp = exp ;
                        this.dateTime = date;
                }







                //public override bool contains_obj(LogicObj o)
                //{
                //    return (lo1 == o || lo2 == o);
                //}
        }

        public class PastActionIsActually: PastAction
        {
                public string completeSentence { get; set; }

                public LogicObj lo;
                
                public Explanation exp1;
                public Explanation exp2;

                public PastActionIsActually(string completeSentence, LogicObj lo, Explanation exp1, Explanation exp2, DateTime date)
                {
                        this.completeSentence = completeSentence;
                        this.lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        
                        this.exp1 = exp1;
                        this.exp2 = exp2;
                        this.dateTime = date;
                }
        }

}
