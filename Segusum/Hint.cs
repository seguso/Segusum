

using System;


// ReSharper disable ReplaceWithSingleCallToFirstOrDefault

namespace Seg
{

        public class HintTextAndContinue
        {
                public HintTextAndContinue(bool canContinue, params string[] htmls)
                {
                        this.canContinue = canContinue;
                        this.htmls = htmls ?? throw new ArgumentNullException(nameof(htmls));
                }

                public bool canContinue { get; set; }
                public string[] htmls { get; set; }

                
        }

        public class Hint
        {
                public Hint(CycleElemId id, Func<HintTextAndContinue> f)
                {
                        this.id = id ?? throw new ArgumentNullException(nameof(id));
                        this.f = f;
                        this.minutesToWait = null;
                }

                public Hint(CycleElemId id)
                {
                        this.id = id ?? throw new ArgumentNullException(nameof(id));
                        ;
                        this.minutesToWait = null;
                }
                public Hint(CycleElemId id, Func<bool> onlyVisibleIf, params string[] htmls)
                {
                        this.id = id ?? throw new ArgumentNullException(nameof(id));
                        f = () =>
                        {
                                return new HintTextAndContinue(true, htmls);
                        };

                        this.OnlyShowIf = onlyVisibleIf;
                        this.minutesToWait = null;
                        
                }

                public Hint(CycleElemId id, params string[] htmls)
                {
                        this.id = id ?? throw new ArgumentNullException(nameof(id));
                        f = () =>
                        {
                                return new HintTextAndContinue(true, htmls);
                        };

                        this.OnlyShowIf = null;
                        this.minutesToWait = null;

                }

                public Hint(CycleElemId id, double minutesToWait, params string[] htmls)
                {
                        this.id = id ?? throw new ArgumentNullException(nameof(id));
                        f = () =>
                        {
                                return new HintTextAndContinue(true, htmls);
                        };


                        this.minutesToWait = minutesToWait;

                }

                //public Hint(CycleElemId id, double minutesToWait, params string[] htmls)
                //{
                //        this.id = id ?? throw new ArgumentNullException(nameof(id));
                //        this.htmls = htmls ?? throw new ArgumentNullException(nameof(htmls));
                //        this.minutesToWait = minutesToWait;
                //        this.condizione = null;
                //}

                public CycleElemId id { get; set; }

                public double? minutesToWait { get; set; }

                public Func<HintTextAndContinue> f { get; set; }

                public Func<bool> OnlyShowIf { get; set; }

                //public string[] htmls { get; set; }
        }
}
