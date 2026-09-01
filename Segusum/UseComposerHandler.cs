using System;

namespace Seg
{
        public class UseInComposerHandler
        {

                public LogicObj lo { get; set; }

                public Template template { get; set; }

                public Filler[] fillers { get; set; }

                public Action<HandlerInput> handler;

                public UseInComposerHandler(LogicObj lo, Template template, Filler filler, Action<HandlerInput> handler)
                {
                        this.lo = lo;
                        this.template = template;

                        this.fillers = new[] { filler };
                        this.handler = handler;
                }

                public UseInComposerHandler(LogicObj lo, Template template, Filler filler1, Filler filler2, Action<HandlerInput> handler)
                {
                        this.lo = lo;
                        this.template = template;

                        this.fillers = new[] { filler1, filler2 };
                        this.handler = handler;
                }
        }

        //public class UnaryVerbNoObjectiveHandler
        //{
        //        public UnVerb unVerb;
        //        public LogicObj lo;

        //        public Action<HandlerInput> handler;

        //        public UnaryVerbNoObjectiveHandler(UnVerb unVerb, LogicObj lo, Action<HandlerInput> handler)
        //        {
        //                this.unVerb = unVerb;
        //                this.lo = lo;
        //                this.handler = handler;
        //        }

        //        public bool containsObj(LogicObj l)
        //        {
        //                return l == lo;
        //        }

        //}
}
