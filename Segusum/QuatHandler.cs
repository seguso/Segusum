using System;

namespace Seg
{
    /// <summary>
    /// example: attach X to Y     (in order to Z)
    /// </summary>
    public class QuatHandler
    {
        public BinVerb binVerb;
        public LogicObj lo1;
        public LogicObj lo2;
        public Objective puzzle;

        public Action<HandlerInput> handler;

        public QuatHandler(BinVerb binVerb, LogicObj lo1, LogicObj lo2, Objective puzzle, Action<HandlerInput> handler)
        {
            this.binVerb = binVerb;
            this.lo1 = lo1;
            this.lo2 = lo2;
            this.puzzle = puzzle;
            this.handler = handler;
        }

        public bool containsObj(LogicObj l)
        {
            return l == lo1 || l == lo2;
        }

    }
}
