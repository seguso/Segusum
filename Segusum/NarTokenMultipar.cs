using System.Collections.Generic;

namespace Seg
{
    public class NarTokenMultipar : CutSceneToken
    {
        public List<string> pars ;


        public NarTokenMultipar(bool canBeSkipped, string img, List<string> pars, bool canGoBackToPrev) : base(canBeSkipped, img, canGoBackToPrev)
        {
            this.pars = pars;
        }
    }
}
