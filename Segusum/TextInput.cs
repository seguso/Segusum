namespace Seg
{
        public class TextInput
        {
                //public List<string> textInputIntroPars;

                public string tiShortTitle;

                public string tiIntroBeforeTextbox;

                public string tiIntroBeforeSecondTextbox;

                public string serId;

                public Explanation tiCorrectExplanation { get; set; }

                public Explanation[] tiVisibleExplanations{ get; set; }

                public string tiPreamboloExplanation { get; set; }

                public TextInput(string serId, string shortTitle, string longIntro,  string introBeforeSecondTextbox = null, string preamboloExplanation = null)
                {
                        //this.textInputIntroPars = textInputIntroPars;
                        this.tiIntroBeforeTextbox = longIntro;
                        this.tiIntroBeforeSecondTextbox = introBeforeSecondTextbox;
                        this.serId = serId;
                        this.tiShortTitle = shortTitle;
                        this.tiPreamboloExplanation = preamboloExplanation;
                        //this.tiVisibleExplanations = tiVisibleExplanations;
                        //this.tiCorrectExplanation = explanation;
                }
        }


        public class TextInputClient
        {
                //public List<string> textInputIntroPars;

                public string tiShortTitle;

                public string tiIntroBeforeTextbox;

                public string tiIntroBeforeSecondTextbox;

                public string serId;

                public ExplanationClient tiCorrectExplanation { get; set; }

                public ExplanationClient[] tiVisibleExplanations { get; set; }

                public string tiPreamboloExplanation { get; set; }

                public TextInputClient(string serId, string shortTitle, string introBeforeTextbox, string introBeforeSecondTextbox = null, string preamboloExplanation = null)
                {
                        //this.textInputIntroPars = textInputIntroPars;
                        this.tiIntroBeforeTextbox = introBeforeTextbox;
                        this.tiIntroBeforeSecondTextbox = introBeforeSecondTextbox;
                        this.serId = serId;
                        this.tiShortTitle = shortTitle;
                        this.tiPreamboloExplanation = preamboloExplanation;
                        //this.tiVisibleExplanations = tiVisibleExplanations;
                        //this.tiCorrectExplanation = explanation;
                }
        }

}
