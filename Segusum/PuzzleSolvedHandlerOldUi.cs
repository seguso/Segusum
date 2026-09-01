using System;

namespace Seg
{
        public class PuzzleSolvedHandlerOldUi
        {

                public PuzzleSolution puzzleSolution; // ci possono essere due soluzioni. ogni soluzione è un array

                public Action<HandlerInput> handler;

                public PuzzleSolvedHandlerOldUi(PuzzleSolution puzzleSolution, Action<HandlerInput> handler)
                {
                        
                        this.puzzleSolution = puzzleSolution ?? throw new ArgumentNullException(nameof(puzzleSolution));
                        this.handler = handler;
                }

                public override string ToString()
                {
                        return puzzleSolution.ToString();
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
