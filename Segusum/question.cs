using System;

namespace Seg
{
    public class Question
    {
        public string[] visibleIfReadAllOf = new string[] { };
        public string[] obsoleteIfReadAnyOf = new string[] { };
        public string id;
        public string questionText;
        public bool asked = false;
        public Action<ResponseInput> response;
        //public bool exitDialogAfterThis = false;



    }
}
