namespace Seg
{
    public class PastActionMove : PastAction
    {

        public Room room;

        //public override bool contains_obj(LogicObj o)
        //{
        //    return false;
        //}
    }


        public class PastActionSolvePuzzle: PastAction
        {
                public string Solution { get; set; }

                
        }

        public class PastActionSubmitText: PastAction
        {
                public string TextTyped { get; set; }

                public string TextTyped2 { get; set; }


                public string explId { get; set; }


        }

        public class PastActionCancelText: PastAction
        {
                


        }



}
