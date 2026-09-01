namespace Seg
{
        //public class BinActionInput : credentials
        //{

        //    public string baiBinVerbId;
        //    public string baiLoId;

        //}


        public class UseWithActionInput : Credentials
        {

                public string uwaLoId1;
                public string uwaLoId2;

                public bool uwaAlreadyKnowItFails;

                public string uwaExplanationId;

        }


        public class IsActuallyInput : Credentials
        {

                public string iaLoId;
                
                public string iaExp1Id { get; set; }
                public string iaExp2Id { get; set; }

        }

        public class UseForInput : Credentials
        {

                public string ufiLoId;
                public string ufiObjId;
                public string ufiExpId { get; set; }

        }

        public class TutorialPromptInput : Credentials
        {
                public string tpiKind;
                public string tpiFirstObjectId;
                public string tpiSecondObjectId;
        }

        public class cinComposer
        {

                public string cinText { get; set; }
                public bool cinCliccabile { get; set; }
                public string cinFiId { get; set; }
        }

        public class UseInComposerInput: Credentials
        {

                public string uwcLoId;
                public string uwcTemplateId;
                public string uwcFillerId1;
                public string uwcFillerId2;

                public cinComposer[] uwcPezzi{ get; set; }
        }

        public class UnActionInput : Credentials
    {

        public string uaiZeroVerbId;
                

    }

        public class PuzzleSolutionPieceSentByClient
        {
                public bool isEnu;
                public string qt_serId; // se è un enumerated token

                public string oir_loIdCorrect; // se è un room token

                public string psi_readableName;
        }

        public class PuzzleSolutionInput : Credentials
        {

                public ObjectiveClient psi_objective;

                public PuzzleSolutionPieceSentByClient[] psi_solutionSent;


        }
        public class AutoSolvePuzzleInput : Credentials
        {

                public ObjectiveClient psi_objective;



        }


        //public class verbInfo
        //{
        //    public string verbId;

        //    public bool isUnary;

        //    public bool invertObjectOrder;

        //    public string secondPart;
        //    public string firstPartForSentence;
        //    public string stringForContextMenu;

        //}

        //public class topicInfo
        //{
        //    public string topicId;
        //    public string questionText;
        //}

        //public class objTopicInfo
        //{
        //    //public string loId; // se parli con questo personaggio, allora i topic sono questi:


        //    public string topicId;

        //    public string questionText; // the text is different for every character you ask. Example: "{1}, how are you?"

        //    public override bool Equals(object obj)
        //    {
        //        var ot = obj as objTopicInfo;
        //        if (ot != null)
        //        {
        //            return ot.topicId == topicId && ot.questionText == questionText;
        //        }
        //        return false;
        //    }

        //    public override int GetHashCode()
        //    {
        //        return
        //            //(loId.GetHashCode().ToString() + 
        //            (topicId.GetHashCode().ToString() + questionText.GetHashCode().ToString()).GetHashCode();
        //    }

        //} // end class




}
