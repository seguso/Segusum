namespace Seg
{
        public class EnumeratedToken : PuzzleToken
        {


                public Qtok correct;

                public Qtok[] choices;

                
                public EnumeratedToken(Qtok correct, Qtok[] choices)
                {
                        this.correct = correct ;
                        this.choices = choices;
                        
                }


                //public EnumeratedToken(Qtok[] correct, Qtok[] choices)
                //{
                //        this.correct = correct;
                //        this.choices = choices;
                //}

                public override string ToString()
                {
                        return correct.serId;
                }
        }
}
