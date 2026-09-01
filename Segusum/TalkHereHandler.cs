using System;

namespace Seg
{
        public class TalkHereHandler
        {
                public Room room { get; set; }
                public Action<HandlerInput> handler;
        }
}
