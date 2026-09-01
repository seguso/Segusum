

using System;
using System.Collections.Generic;
using System.Linq;


// ReSharper disable ReplaceWithSingleCallToFirstOrDefault

namespace Seg
{
        public class ObjectiveAndHints
        {
                public ObjectiveAndHints(Objective ob, IEnumerable<Hint> hints)
                {
                        this.ob = ob ?? throw new ArgumentNullException(nameof(ob));
                        this.hints = hints.ToArray();
                }

                public Objective ob { get; set; }

                public Hint[] hints { get; set; }

        }
}
