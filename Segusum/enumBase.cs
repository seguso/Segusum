using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Seg
{
    public class enumBase<T> where T : struct
    {
        private T value;
        public void Foo(T value)
        {
            this.value = value;
        }
    }
}
