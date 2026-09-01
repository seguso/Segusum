using System;

namespace Seg
{
        //public class UseWithHandler
        //{
        //    public BinVerb binVerb;
        //    public LogicObj lo1;
        //    public LogicObj lo2;
        //    public Objective puzzle;
        //    public Action<HandlerInput> handler;

        //    public UseWithHandler(BinVerb binVerb, LogicObj lo1, LogicObj lo2, Objective puzzle, Action<HandlerInput> handler)
        //    {
        //        this.binVerb = binVerb;
        //        this.lo1 = lo1;
        //        this.lo2 = lo2;
        //        this.puzzle = puzzle;
        //        this.handler = handler;
        //    }

        //    public bool containsObj(LogicObj l)
        //    {
        //        return l == lo1 || l == lo2;
        //    }

        //}

        
        public class UseForHandler
        {
                public UseForHandler(LogicObj lo, Objective objective, Action<HandlerInput> handler, Explanation explanation)
                {
                        Lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        Objective = objective ?? throw new ArgumentNullException(nameof(objective));
                        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
                        Explanation = explanation ;
                }

                public LogicObj Lo { get; set; } 
                public Objective Objective { get; set; }
                public Action<HandlerInput> Handler { get; set; }
                public Explanation Explanation { get; set; }



        }

        public class IsActuallyHandler
        {
                public LogicObj Lo;

                public IsActuallyHandler(LogicObj lo, Explanation explanation1, Explanation explanation2, Action<HandlerInput> handler)
                {
                        this.Lo = lo ?? throw new ArgumentNullException(nameof(lo));
                        Explanation1 = explanation1 ?? throw new ArgumentNullException(nameof(explanation1));
                        Explanation2 = explanation2 ?? throw new ArgumentNullException(nameof(explanation2));

                        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
                }

                public Explanation Explanation1 { get; set; }

                public Explanation Explanation2 { get; set; }

                public Action<HandlerInput> Handler { get; set; }
        }


        public class CombineHandler
        {
           
                public LogicObj lo1;
                public LogicObj lo2;
                public string SentenceUntransl { get; set; }
                public Action<HandlerInput> handler;

                public Func<string> DynamicSentence { get; set; }

                public Func<bool> IsPossibleNow { get; set; }

                public Explanation Explanation { get; set; }

                public CombineHandler(LogicObj lo1, LogicObj lo2, string sentenceUntransl, Action<HandlerInput> handler, Func<bool> isPossibleNow , Explanation explanation)
                {
                        this.lo1 = lo1;
                        this.lo2 = lo2;
                        this.SentenceUntransl = sentenceUntransl;
                        this.DynamicSentence = null;
                        this.handler = handler;
                        this.IsPossibleNow = isPossibleNow;
                        this.Explanation = explanation;
                }

                public CombineHandler(LogicObj lo1, LogicObj lo2, Func<string> dynamicSentence, Action<HandlerInput> handler, Func<bool> isPossibleNow, Explanation explanation )
                {
                        this.lo1 = lo1;
                        this.lo2 = lo2;
                        this.SentenceUntransl = null;
                        this.DynamicSentence = dynamicSentence;
                        this.handler = handler;
                        this.IsPossibleNow = isPossibleNow;
                        this.Explanation = explanation;
                }

                public bool containsObj(LogicObj l)
                {
                        return l == lo1 || l == lo2;
                }

        }


        public class LookHandler
        {

                public LogicObj lo1;
                public Action<HandlerInput> handler;

                public Func<bool> IsLookableNow;

                public Func<string> DynamicSentence;

                public LookHandler(LogicObj lo1, Action<HandlerInput> handler, Func<bool> isLookableNow, Func<string> dynamicSentence)
                {
                        this.lo1 = lo1;
                        this.handler = handler;
                        this.IsLookableNow = isLookableNow;
                        this.DynamicSentence = dynamicSentence;
                }

                public bool containsObj(LogicObj l)
                {
                        return l == lo1 ;
                }

        }


        public class PickUpHandler
        {

                public LogicObj lo1;
                public Action<PickUpHandlerInput> handler;

                public PickUpHandler(LogicObj lo1, Action<PickUpHandlerInput> handler)
                {
                        this.lo1 = lo1;
                        this.handler = handler;
                }

                public bool containsObj(LogicObj l)
                {
                        return l == lo1;
                }

        }



}
