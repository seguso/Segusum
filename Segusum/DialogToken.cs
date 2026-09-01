using System.Data.SqlTypes;

namespace Seg
{
        public class DialogToken : CutSceneToken
        {
                public string dtCharName;
                public string dtPar;

                public NarSize ntSize;

                public DialogToken(bool canBeSkipped, string img, string charName, string par, bool canGoBackToPrev, NarSize size) : base(canBeSkipped, img, canGoBackToPrev)
                {
                        this.dtCharName = charName;
                        this.dtPar = par;
                        this.ntSize = size;
                }
        }
}
