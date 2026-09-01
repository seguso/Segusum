using System.Collections.Generic;

namespace Seg
{
    public class ResponseInput
    {
        public CutScene cs ;
        public ConversationRes res /*= conversation_res.ContinueDialog*/;
        public Character charAsking;

        public ResponseInput(CutScene cs, ConversationRes res, Character charAsking)
        {
            this.cs = cs;
            this.res = res;
            this.charAsking = charAsking;
        }
    }
}
