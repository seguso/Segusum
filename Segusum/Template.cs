namespace Seg
{
        public class Template
        {
                public Template(string teId, string heShe, string they, bool IsForSayVerb = false, bool isForChars = false)
                {
                        this.teId = teId;
                        this.heShe = heShe;
                        this.they = they;
                        this.IsForSayVerb = IsForSayVerb;

                        this.isForChars = isForChars;
                }

                public bool isForChars ;


                public bool IsForSayVerb { get; set; }
                public string teId { get; set; }
                public string heShe { get; set; }

                public string they{ get; set; }

                public override string ToString()
                {
                        return heShe;
                }
        }
}
