namespace Seg
{
        //public abstract class parHtmlElClient
        //{
        //    public abstract bool isUseless();
        //    public abstract string textOnlyVersion();
        //}

        //public class simpleTextClient : parHtmlElClient
        //{
        //    public string s;

        //    public override bool isUseless()
        //    {
        //        return s == "";
        //    }

        //    public override string textOnlyVersion()
        //    {
        //        return s;
        //    }

        //    //public override string tostr()
        //    //{
        //    //    return s;
        //    //}
        //}

        public class VerbForClient
        {
                public bool vfcIsBinary;
                public bool vfcIsHighlighted;
                public bool vfcIsUnary;
                public bool vfcCanBeUnaryOrBinaryDependingOnObject = false;
                public bool vfcIsZeroVerb = false;
                public bool vfcCanOnlyBeUsedWithObjsInRoomNotInv;
                public string vfcName;
                public bool vfcIsAskForHints = false;
                public string vfcSecondPart;
                public string vfcSerId;
                public bool vfcRequiresPuzzle = true;
                public int vfcPriority;
                public bool vfcCharIsAlwaysLast = false;
                public bool vfcCharIsAlwaysFirst = false;
                public bool vfc_is_remember = false;
                public bool vfcIsPickup = false;

                public VerbForClient(bool vfcIsBinary, bool vfcIsHighlighted, bool vfcIsUnary, bool vfcCanBeUnaryOrBinaryDependingOnObject, bool vfcIsZeroVerb, bool vfcCanOnlyBeUsedWithObjsInRoomNotInv, string vfcName, bool vfcIsAskForHints, string vfcSecondPart, string vfcSerId, bool vfcRequiresPuzzle, int vfcPriority, bool vfcCharIsAlwaysLast, bool vfcCharIsAlwaysFirst, bool vfc_is_remember, bool vfcIsPickup)
                {
                        this.vfcIsPickup = vfcIsPickup;
                        this.vfcIsBinary = vfcIsBinary;
                        this.vfcIsHighlighted = vfcIsHighlighted;
                        this.vfcIsUnary = vfcIsUnary;
                        this.vfcCanBeUnaryOrBinaryDependingOnObject = vfcCanBeUnaryOrBinaryDependingOnObject;
                        this.vfcIsZeroVerb = vfcIsZeroVerb;
                        this.vfcCanOnlyBeUsedWithObjsInRoomNotInv = vfcCanOnlyBeUsedWithObjsInRoomNotInv;
                        this.vfcName = vfcName;
                        this.vfcIsAskForHints = vfcIsAskForHints;
                        this.vfcSecondPart = vfcSecondPart;
                        this.vfcSerId = vfcSerId;
                        this.vfcRequiresPuzzle = vfcRequiresPuzzle;
                        this.vfcPriority = vfcPriority;
                        this.vfcCharIsAlwaysLast = vfcCharIsAlwaysLast;
                        this.vfcCharIsAlwaysFirst = vfcCharIsAlwaysFirst;
                        this.vfc_is_remember = vfc_is_remember;
                }
        }


        //public class parHtmlClient
        //{
        //    public List<parHtmlElClient> elements = new List<parHtmlElClient>();


        //    public string textOnly()
        //    {
        //        var x = new List<string>();
        //        foreach (var el in elements)
        //        {
        //            x.Add(el.textOnlyVersion());
        //        }

        //        return x.Aggregate((a, b) => a + b);
        //    }

        //    //var r = MakeClickableRun(nomeOgg);

        //    ////r.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 155));


        //    //r.PreviewMouseLeftButtonDown +=  (o, args) =>
        //    //{
        //    //    args.Handled = true;
        //    //    var lo = pairs.Where(pa => pa.pos == pos).Select(pa => pa.lo).Single();
        //    //    Debug.Assert(lo != null);
        //    //    if (qualcheTaskStaAspettandoClicSecondoOggetto != null)
        //    //    {
        //    //        qualcheTaskStaAspettandoClicSecondoOggetto.TrySetResult(lo);
        //    //    }
        //    //    else
        //    //    {
        //    //        // era il clic sul primo ogg
        //    //         ShowVerbMenuForObject(args, lo);
        //    //    }
        //    //};
        //    //return ret;

        //}

}
