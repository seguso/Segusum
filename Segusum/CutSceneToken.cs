using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public abstract class CutSceneToken
    {
        public CutSceneToken add(List<CutSceneToken> l) {

            l.Add(this);
            return this;
        }

        protected  CutSceneToken(bool canBeSkipped, string img, bool  canGoBackToPrev)
        {
            this.cstCanBeSkipped = canBeSkipped;
            this.img = img;
            this.cstCanGoBackToPrevious = canGoBackToPrev;
        }

        public string img;
        public bool cstCanBeSkipped ;
        public bool cstCanGoBackToPrevious;
    }
}
