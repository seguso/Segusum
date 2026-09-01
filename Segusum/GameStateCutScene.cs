using System;

namespace Seg
{
        public class GameStateCutScene : GameState
        {

                public CutScene cs;

                public int iCurToken = 0; // quale pezzo di cut scene è visualizzato ora nel client

                /// <summary>
                /// questo serve perché , se sei in un dialogo, dopo la cutscene devi tornare dentro al dialogo. E serve anche nel caso in cui c'è una cutscene che, 
                /// alla fine, deve presentare delle domande opzionali, tagliate dal testo originale perché rischiavano di essere noiose.
                /// </summary>
                public GameStateShowingQuestions afterCutSceneShowDialog;

                public GameStateWaitingForText afterCutSceneWaitForTextInput;

                public GameStateFinished afterCutSceneGameFinished;

                public GameStateCutScene(CutScene cs, int iCurToken, GameStateShowingQuestions afterCutSceneShowDialog, GameStateWaitingForText afterCutSceneWaitForTextInput
                        , GameStateFinished afterCutSceneGameFinished)
                {
                        this.cs = cs ;
                        this.iCurToken = iCurToken;
                        this.afterCutSceneShowDialog = afterCutSceneShowDialog;
                        this.afterCutSceneWaitForTextInput = afterCutSceneWaitForTextInput ;
                        this.afterCutSceneGameFinished = afterCutSceneGameFinished;
                        
                }

                

        }
}
