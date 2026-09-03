namespace Seg
{
        /// <summary>
        /// posso registrarmi o con email e pwd, o con il token generato dal server.
        /// </summary>
    public class Credentials
        {
                public string uname;
                public string pwd;
                //public string token;

                public string lang;

                public ulong? curTime { get; set; }


        public int? cred_gameId;

        // Seleziona il mini-mondo tutorial e il relativo spazio di salvataggio.
        public bool tutorialMode { get; set; }
    }

    public sealed class AdminNarrativeSeenInput : Credentials
    {
        public long[] messageIds { get; set; } = Array.Empty<long>();
    }

        public class GameModeInput : Credentials
        {
                public bool casualMode { get; set; }
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
