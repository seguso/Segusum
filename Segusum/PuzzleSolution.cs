using System.Linq;

namespace Seg
{
        public class PuzzleSolution
        {

                public override string ToString()
                {
                        return solution.Select(x => x.ToString()).aggregateStringList();
                }
                public Objective objective;
                public PuzzleToken[] solution;

                public PuzzleSolution(Objective objective, PuzzleToken[] solution)
                {
                        this.objective = objective;

                        
                        foreach(var x in solution)
                        {
                                if (x is EnumeratedToken ent)
                                {
                                        // se la corretta non è tra le scelte, aggiungila
                                        ent.choices = ent.choices.Append(ent.correct).Distinct().ToArray();
                                }
                        }

                        this.solution = solution;
                }
        }
}
