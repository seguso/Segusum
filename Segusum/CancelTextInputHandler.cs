using System;

namespace Seg
{
        public class CancelTextInputHandler
        {
                public TextInput ti;

                public Action<HandlerInput> handler;

                public CancelTextInputHandler(TextInput ti, Action<HandlerInput> handler)
                {
                        this.ti = ti;
                        this.handler = handler;
                }
        }
}
