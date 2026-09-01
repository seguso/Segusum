using System;

namespace Seg
{


        public class AutoSolvePuzzleHandler
        {

                public Objective objective; // ci possono essere due soluzioni. ogni soluzione è un array

                public Action<HandlerInput> handler;

                public AutoSolvePuzzleHandler(Objective objective, Action<HandlerInput> handler)
                {

                        this.objective = objective;
                        this.handler = handler;
                }

                public override string ToString()
                {
                        return objective.serId;
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
