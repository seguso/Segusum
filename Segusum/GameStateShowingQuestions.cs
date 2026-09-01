namespace Seg
{
    public class GameStateShowingQuestions : GameState
    {
        public Dialog dialog;
    }

        public class GameStateWaitingForText: GameState
        {
                public TextInput textInput;

                public GameStateWaitingForText(TextInput textInput)
                {
                        this.textInput = textInput;
                }
        }


        public class GameStateFinished  : GameState
        {

        }
}
