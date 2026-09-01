namespace Seg
{
        public class ObjInRoomToken: PuzzleToken
        {
                public LogicObj correct;

                public ObjInRoomToken(LogicObj correct)
                {
                        this.correct = correct;
                }

                
                public override string ToString()
                {
                        return correct.loId;
                }
        }
}
