namespace Seg
{
    public class QuatActionInput : Credentials
    {

        public string qaiBinVerbId;
        public string qaiLo1Id;
        public string qaiLo2Id;
        public string qaiPuzId;

    }


    public class BinaryNoObActionInput : Credentials
    {

        public string bnaiBinVerbId;
        public string bnaiLo1Id;
        public string bnaiLo2Id;

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
