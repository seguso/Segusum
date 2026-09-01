using System;

namespace Seg
{

        public class SubmitTextInputHandler
        {
                public TextInput ti;
                

                public Action<TextHandlerInput> handler;

                public SubmitTextInputHandler(TextInput ti, Action<TextHandlerInput> handler)
                {
                        this.ti = ti;
                        this.handler = handler;
                }
        }
}
