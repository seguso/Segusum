using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    //public class TerHandlerUn
    //{
    //    public UnVerb unVerb;
    //    public LogicObj lo;
    //    public Objective puzzle;

    //    public Action<HandlerInput> handler;


    //    public bool containsObj(LogicObj l)
    //    {
    //        return l == lo ;
    //    }

    //}

        public class UseInLocationHandler
        {
                
                public LogicObj lo;

                //public Room room;

                public BinVerb binVerb;

                public Action<HandlerInput> handler;

                public UseInLocationHandler(LogicObj lo, /*Room room, */BinVerb binVerb, Action<HandlerInput> handler)
                {
                        this.lo = lo;
                        //this.room = room;
                        this.binVerb = binVerb;
                        this.handler = handler;
                }

                public bool containsObj(LogicObj l)
                {
                        return l == lo;
                }

        }
}
