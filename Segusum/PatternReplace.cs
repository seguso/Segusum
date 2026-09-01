using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
        public class PatternReplace
        {
                public PatternReplace(string word1, string word2, string repl, bool matchStartOfSecondWord = false)
                {
                        Word1 = word1;
                        Word2 = word2;
                        Repl = repl;

                        MatchStartOfSecondWord = matchStartOfSecondWord;
                }

                public bool MatchStartOfSecondWord { get; set; }

                public string Word1 { get; set; }

                public string Word2 { get; set; }

                public string Repl { get; set; }
        }
}
