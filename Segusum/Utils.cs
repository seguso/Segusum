using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Seg
{
        public static class Utils
        {

                public static string stringOfDateCentesimi(DateTime d)
                {
                        var decimi = (int)(((double)d.Millisecond) / 10.0);
                        var decimiStr = decimi.ToString();
                        return d.ToLongTimeString() + ":" + decimiStr;
                }

                public static X retry<X>(Func<X> f)
                {

                        //return f();
                        int conto = 0;
                        List<string> eccezioni = new List<string>();

                        ancora:
                        try
                        {



                                var ret = f();

                                if (eccezioni.Any()) // ho riprovato qualche volta
                                {
                                        eccezioni.Insert(0, $"\nHa funzionato riprovando {conto} volte: ecco le eccezioni quando non ha funzionato: \n");

                                        var strDaLoggare = eccezioni.aggregateStringList(sep: "------ ");

                                        //Log.printToLogGeneric(strDaLoggare, $"Retry_success_dopo_{conto}_");
                                }

                                return ret;
                        }
                        catch (Exception e)
                        {
                                var erroreStr = UtilsW.stringOfException(e);

                                eccezioni.Add($"\nErrore: {DateTime.Now.ToString()} : tentativo numero {conto}: dettagli : \n{erroreStr}");

                                System.Threading.Thread.Sleep(400);

                                if (conto < 4)
                                {
                                        conto++;
                                        goto ancora;
                                }
                                else
                                {
                                        eccezioni.Insert(0, $"\nSmetto di riprovare: già riprovato {conto} volte. Ecco gli errori: \n");

                                        var strDaLoggare = eccezioni.aggregateStringList(sep: "------ ");

                                        //Log.printToLogGeneric(strDaLoggare, "Retry_fail_");
                                        throw;
                                }

                        }

                }



                public static bool neverHappened(this DateTime t)
                {
                        return t == default(DateTime);
                }

                public static bool itHappened(this DateTime t)
                {
                        return t != default(DateTime);
                }

                public static void setIfNeverHappened(ref DateTime  t)
                {
                        if (t.neverHappened())
                        {
                                t = DateTime.Now;
                        }
                }

                public static int quickIntHashOfString(this string s)
                {
                        var sum = s.Select(ch => (int)ch).Sum();
                        return sum;
                }
                public static string fixCrLf(this string s)
                {
                        return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
                }

                public static void printToLogGeneric(string s, string logFileName, dynamic Request = null /* se non è null mette nel log la querystring */)
                {
                        try
                        {
                                var space = "            ";
                                var date = DateTime.Now;
                                var dateStr = date.ToShortDateString();
                                var timeStr = (stringOfDateCentesimi(date));
                                var dateAndTimeStr = dateStr + " " + timeStr;

                                //var dataAndTimeStrForFileName = stripAllCharsWhichAreNotLettersOrDigits(dateAndTimeStr);
                                var dataStr = DateTime.Now.ToString("yyyy-MM-dd");

                                string datiQueryStringEBrowser;

                                if (Request == null)
                                {
                                        datiQueryStringEBrowser = "";
                                }
                                else
                                {
                                        datiQueryStringEBrowser = $"Querystring = {Request.Url.Query} --- browser = {Request.Browser.Browser}    ";
                                }


                                s = fixCrLf(s);

                                s = space + s.Replace($"{Environment.NewLine}", $"{Environment.NewLine}{space}"); // faccio indent nel caso dentro s ci siano a capi

                                var fullStr = Environment.NewLine + Environment.NewLine + dateAndTimeStr + space + datiQueryStringEBrowser + Environment.NewLine + s + Environment.NewLine + Environment.NewLine + "_____________________________________________________";

                                const string dirName = "~/Log";
                                var dirPath = MapPathCrossHost(dirName);
                                System.IO.Directory.CreateDirectory(dirPath); // se esiste già non fa niente

                                var fileNAme = $"~/Log/{logFileName}_{dataStr}.txt";
                                var logPath = MapPathCrossHost(fileNAme);
                                using (var stream = System.IO.File.AppendText(logPath))
                                {
                                        stream.Write(fullStr);

                                }
                        }
                        catch
                        {
                        }

                }
                public static string MapPathCrossHost(string appRelativePath)
                {
                        var normalizedRelative = normalizeAppRelativePath(appRelativePath);

                        foreach (var basePath in enumerateCandidateWebRoots())
                        {
                                var full = Path.Combine(basePath, normalizedRelative);
                                if (File.Exists(full) || Directory.Exists(full))
                                {
                                        return full;
                                }
                        }

                        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, normalizedRelative);
                }

                private static string normalizeAppRelativePath(string appRelativePath)
                {
                        var rel = appRelativePath ?? string.Empty;
                        rel = rel.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                        if (rel.StartsWith("~" + Path.DirectorySeparatorChar))
                        {
                                rel = rel.Substring(2);
                        }
                        else if (rel.StartsWith("~"))
                        {
                                rel = rel.Substring(1);
                        }

                        rel = rel.TrimStart(Path.DirectorySeparatorChar);
                        return rel;
                }

                private static IEnumerable<string> enumerateCandidateWebRoots()
                {
                        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var candidates = new List<string>();

                        void yieldIfAny(string p)
                        {
                                if (string.IsNullOrWhiteSpace(p))
                                {
                                        return;
                                }
                                if (!Directory.Exists(p))
                                {
                                        return;
                                }
                                if (yielded.Add(p))
                                {
                                        candidates.Add(p);
                                }
                        }

                        yieldIfAny(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"));
                        yieldIfAny(AppDomain.CurrentDomain.BaseDirectory);
                        yieldIfAny(Directory.GetCurrentDirectory());

                        var start = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                        while (start != null)
                        {
                                // Cerca una web root per convenzione, senza conoscere il nome
                                // del gioco che ospita il motore.
                                IEnumerable<string> webRoots;
                                try
                                {
                                        webRoots = Directory.EnumerateDirectories(start.FullName, "wwwroot");
                                }
                                catch (UnauthorizedAccessException)
                                {
                                        // Un host può risalire fino a una directory padre
                                        // non leggibile (tipico in Termux): quella radice
                                        // non è una candidata, ma non deve interrompere il
                                        // fallback delle radici già trovate.
                                        webRoots = Array.Empty<string>();
                                }

                                foreach (var webRoot in webRoots)
                                {
                                        var webProjectFolder = Directory.GetParent(webRoot)?.FullName;
                                        yieldIfAny(webProjectFolder);
                                }

                                if (File.Exists(Path.Combine(start.FullName, "Web.config")) &&
                                    Directory.Exists(Path.Combine(start.FullName, "Views")))
                                {
                                        yieldIfAny(start.FullName);
                                }

                                start = start.Parent;
                        }

                        return candidates;
                }
                public static bool Any<X, Y>(this IEnumerable<X> l, Func<X, bool> f, out Y eltrovato) where Y : X
                {
                        foreach (X el in l)
                        {
                                bool bo = f(el);
                                if (bo)
                                {
                                        if (el is Y ely)
                                        {

                                                eltrovato = ely;
                                                return true;
                                        }
                                }

                        }
                        eltrovato = default(Y);
                        return false;
                }


                public static bool isEmpty<T>(this IEnumerable<T> l)
                {
                        return !l.Any();
                }


                public static bool is_not_null_or_white(this string s)
                {
                        return !String.IsNullOrWhiteSpace(s);
                }

                public static bool isNullOrWhite(this string s)
                {
                        return String.IsNullOrWhiteSpace(s);
                }
                //public static annotatedString toan(this string s, translationHint trh)
                //{
                //    return new annotatedString { s = s, trh = trh };
                //}

                //public static string tr(this annotatedString s)
                //{
                //    s.s = s.s.Trim();
                //    // todo traduci

                //    return s.s;
                //}


                public static Character[] toArr(this Character ch)
                {
                        return new Character[] { ch };
                }

                //public static parHtmlServer[] toArr(this parHtmlServer ch)
                //{
                //    return new parHtmlServer[] { ch };
                //}


                public static string tr(this string s, string inst = null)
                {

                        s = s.Trim();
                        //if (!s.EndsWith(" "))
                        //    s += " ";

                        // todo leggi la traduzione dalle risorse
                        if (inst != null)
                                return s.inst(inst);

                        return s;

                }

                //public static Paragraph tp(this string s, string inst = null)
                //{

                //    var x = s.tr(inst);


                //    return x.topar();
                //}


                public static string inst(this string s, string inst)
                {
                        Debug.Assert(inst != null);
                        var i = s.IndexOf("{1}");

                        if (i == -1)
                        {
                                i = s.IndexOf("{2}");
                                if (i == -1)
                                {
                                        i = s.IndexOf("{3}");
                                        if (i == -1)
                                        {
                                                i = s.IndexOf("{4}");
                                                if (i == -1)
                                                {
                                                        i = s.IndexOf("{5}");
                                                        if (i == -1)
                                                        {

                                                                i = s.IndexOf("{6}");
                                                                if (i == -1)
                                                                {
                                                                        return s;
                                                                }
                                                                else
                                                                {
                                                                        return s.Remove(i, 3).Insert(i, inst);
                                                                }
                                                        }
                                                        else
                                                        {
                                                                return s.Remove(i, 3).Insert(i, inst);
                                                        }

                                                }
                                                else
                                                {
                                                        return s.Remove(i, 3).Insert(i, inst);
                                                }

                                        }
                                        else
                                        {
                                                return s.Remove(i, 3).Insert(i, inst);
                                        }
                                }
                                else
                                {
                                        return s.Remove(i, 3).Insert(i, inst);
                                }
                        }
                        else
                        {
                                return s.Remove(i, 3).Insert(i, inst);
                        }
                }


                //public static void MaybeAdd(this List<verb> ret, verb n)
                //{
                //    if (!ret.Any(n2 => n == n2))
                //        ret.Add(n);
                //}


                //public static string flu(this string str)
                //{
                //    if (str == null)
                //        return null;

                //    if (str.Length > 1)
                //        return char.ToUpper(str[0]) + str.Substring(1);

                //    return str.ToUpper();
                //}

                public static HashSet<T> to_hashset<T>(this IEnumerable<T> l)
                {
                        return new HashSet<T>(l);

                }


                //public static string nameOfObjInWorldDesc2(this logicObjE o, bool det)
                //{

                //    //Debug.Assert(o.asPickable != null || o is character); 

                //    //if ( det && o.subjDetAsAcCallsHim() != null)
                //    //{
                //    //    return o.subjDetAsAcCallsHim(); // se c'è "la valigia di mark" o "la tua valigia", quesot prevale
                //    //}
                //    //else
                //    //{
                //    //    if (o is characterE)
                //    //    {
                //    //        var c = (characterE)o;

                //    //        var ac = c.wo.ac;
                //    //        return ac.howHeCallsSomeoneElseAsSubject(c, det: det);
                //    //    }
                //    //    else
                //    //    {
                //            // è probabilmente un concetto.
                //            if (det)
                //                return o.subjDet;
                //            else
                //                return o.subjInd();
                //    //    }
                //    //}

                //}

                public static string toStr(this IEnumerable<LogicObj> chars)
                {



                        var charsA = chars.ToArray();

                        Debug.Assert(charsA.Length > 0);

                        if (charsA.Length == 1)
                        {
                                return charsA.First().name;
                        }
                        else

                        {



                                var str = charsA.Select(ch => ch.name).Aggregate((a, b) => a + ", " + b);


                                var i = str.LastIndexOf(',');
                                str = str.Remove(i, 1).Insert(i, " " + "e".tr());

                                return str;
                        }

                }

                //public static parHtmlServer concat(this parHtmlServer p1, parHtmlServer p2, string separator = null)
                //{
                //    var p = new parHtmlServer();

                //    p.elements.AddRange(p1.elements.ToList());

                //    if (separator != null)
                //        p.elements.Add(new simpleText { s = separator });

                //    p.elements.AddRange(p2.elements.ToList());

                //    //p.Inlines.AddRange(p1.Inlines.ToList());

                //    //if (separator != null)
                //    //    p.Inlines.Add(separator);

                //    //p.Inlines.AddRange(p2.Inlines.ToList());
                //    return p;
                //}

                //public static parHtmlServer concat(this parHtmlServer p1, string s)
                //{
                //    var p = new parHtmlServer();
                //    p.elements.AddRange(p1.elements.ToList());


                //    p.elements.Add(new simpleText { s = s });


                //    return p;
                //}

                //public static parHtmlServer concat(this string s, parHtmlServer p1)
                //{
                //    var p = new parHtmlServer();
                //    p.elements.Add(new simpleText { s = s });
                //    p.elements.AddRange(p1.elements.ToList());


                //    return p;
                //}

                //public static parHtmlServer concat(this string s, string s2)
                //{
                //    var p = new parHtmlServer();
                //    p.elements.Add(new simpleText { s = s });
                //    p.elements.Add(new simpleText { s = s2 });


                //    return p;
                //}


                //public static parHtmlServer toPar(this IEnumerable<logicObjE> objs, bool det)
                //{

                //    var objsAr = objs.ToArray();


                //    var pars = new List<parHtmlServer>();
                //    foreach (var o in objsAr)
                //    {
                //        var str = "[" + o.name + "|1]";
                //        var par = str.topar(o);
                //        pars.Add(par);
                //    }


                //    return pars.aggregatePars(", ", " " + "e".tr() + " ");


                //    //var str = charsA.Select(ch => ch.nameReadable).Aggregate((a, b) => a + ", " + b);



                //    //var i = str.LastIndexOf(',');
                //    //str = str.Remove(i, 1).Insert(i, " " + "e".tr());

                //    //return str;


                //}

                //public static parHtmlServer aggregatePars(this List<parHtmlServer> pars, string sep, string lastSep)
                //{
                //    Debug.Assert(pars.Count > 0);

                //    if (pars.Count == 1)
                //    {

                //        {
                //            return pars.Single();
                //        }
                //    }
                //    else
                //    {
                //        if (pars.Count == 2)
                //        {

                //            {
                //                return pars.First().concat(lastSep).concat(pars.Last());
                //            }
                //        }
                //        else
                //        {
                //            var last = pars.Last();
                //            var others = pars.Where(pa => pa != last).ToList();

                //            var agg = others.Aggregate((p1, p2) => p1.concat(p2, sep));


                //            return agg.concat(lastSep).concat(last);
                //        }
                //    }
                //}


                //public static parHtml topar(this string s, List<eng.pairLoPos> pairs)
                //{
                //    return eng.parOfString(s, pairs);
                //}

                //public static parHtmlServer topar(this string s)
                //{
                //    var q = new List<eng.pairLoPos>();

                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo)
                //{
                //    Debug.Assert(lo != null);
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo, pos = 1 });
                //    return eng.parOfString(s, q);
                //}




                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    return eng.parOfString(s, q);
                //}
                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3, logicObjE lo4)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    q.Add(new eng.pairLoPos { lo = lo4, pos = 4 });
                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3, logicObjE lo4, logicObjE lo5, logicObjE lo6, logicObjE lo7)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    q.Add(new eng.pairLoPos { lo = lo4, pos = 4 });
                //    q.Add(new eng.pairLoPos { lo = lo5, pos = 5 });
                //    q.Add(new eng.pairLoPos { lo = lo6, pos = 6 });
                //    q.Add(new eng.pairLoPos { lo = lo7, pos = 7 });

                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3, logicObjE lo4, logicObjE lo5, logicObjE lo6, logicObjE lo7, logicObjE lo8)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    q.Add(new eng.pairLoPos { lo = lo4, pos = 4 });
                //    q.Add(new eng.pairLoPos { lo = lo5, pos = 5 });
                //    q.Add(new eng.pairLoPos { lo = lo6, pos = 6 });
                //    q.Add(new eng.pairLoPos { lo = lo7, pos = 7 });
                //    q.Add(new eng.pairLoPos { lo = lo8, pos = 8 });

                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3, logicObjE lo4, logicObjE lo5)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    q.Add(new eng.pairLoPos { lo = lo4, pos = 4 });
                //    q.Add(new eng.pairLoPos { lo = lo5, pos = 5 });


                //    return eng.parOfString(s, q);
                //}

                //public static parHtmlServer topar(this string s, logicObjE lo1, logicObjE lo2, logicObjE lo3, logicObjE lo4, logicObjE lo5, logicObjE lo6)
                //{
                //    var q = new List<eng.pairLoPos>();
                //    q.Add(new eng.pairLoPos { lo = lo1, pos = 1 });
                //    q.Add(new eng.pairLoPos { lo = lo2, pos = 2 });
                //    q.Add(new eng.pairLoPos { lo = lo3, pos = 3 });
                //    q.Add(new eng.pairLoPos { lo = lo4, pos = 4 });
                //    q.Add(new eng.pairLoPos { lo = lo5, pos = 5 });
                //    q.Add(new eng.pairLoPos { lo = lo6, pos = 6 });


                //    return eng.parOfString(s, q);
                //}

                //public static void Add(this List<par> p,  string s)
                //{
                //    p.Add(s.topar());
                //}

                public static X random_or_default<X>(this IEnumerable<X> l)
                {

                        var ar = l.ToArray();
                        if (ar.Any())
                        {
                                var i = eng.rnd.Next() % (ar.Length);
                                return ar[i];
                        }
                        else return default(X);

                }




                //public static nar_token tonar(this string s)
                //{
                //    return new nar_token ( par : s);
                //}




                //public static dialog_token todial(this string s, character c, string img = null)
                //{

                //    var ac = c.wo.ac;
                //    var imgToSet = img ?? c.img_default;
                //    return new dialog_token
                //    {
                //        par = s,
                //        img = imgToSet,
                //        charName = $"{c.name_for_dialog??c.name}. ", 
                //    };
                //}


                //public static cut_scene cs { get { return new List<cut_scene_token>(); } }


                //public static List<parHtmlServer> lp { get { return new List<parHtmlServer>(); } }


                //public static narTokenMultiPar toNarMultiTextOnly(this List<parHtmlServer> l)
                //{
                //    return new narTokenMultiPar { pars = l.Select(p => p.textOnly()).ToList() };
                //}


                public static IEnumerable<T> SelectSome<T>(this IEnumerable<T> l)
                {
                        return l.Where(x => x != null);
                }


                public static bool not_in<X>(this X el, IEnumerable<X> l)
                {
                        return !l.Contains(el);
                }

                public static List<X> and<X>(this X el, X el2)
                {
                        return new List<X> { el, el2 };
                }

                public static List<X> and<X>(this List<X> l, X el)
                {

                        var newl = new List<X>(l) { el }; // shallow copy
                        return newl;


                }
                
                public static MyList<A> ToMyList<A>(this IEnumerable<A> l)
                {
                        return new MyList<A>(l);
                }


                
                public static string firstLetterToUpper(this string s)
                {
                        // Check for empty string.  
                        if (string.IsNullOrEmpty(s))
                        {
                                return string.Empty;
                        }
                        // Return char and concat substring.  
                        return char.ToUpper(s[0]) + s.Substring(1);
                }












                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { action = a });
                        return cyc;

                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, params Action<DateTime?>[] actions)
                {
                        return cyc.addToCycle(Id, x =>
                        {
                                foreach (var action in actions)
                                {
                                        action(x);
                                }
                        });
                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Importance importance, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { action = a, IsImportant = importance == Importance.Important });
                        return cyc;

                }

                //public void addToCycle(Cycle cyc, string Id, Action<DateTime?> a)
                //{
                //        var id = new CycleElemId(Id, this);
                //        cyc.Add(new CycleElement(id) { action = a });
                //        //return cyc;

                //}

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Importance importance, Func<DateTime?, bool> cond, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { cond = cond, action = a , IsImportant = importance == Importance.Important});
                        return cyc;

                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Func<DateTime?, bool> cond, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { cond = cond, action = a });
                        return cyc;

                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Func<DateTime?, bool> cond, params Action<DateTime?>[] actions)
                {
                        return cyc.addToCycle(Id, cond, x =>
                        {
                                foreach (var action in actions)
                                {
                                        action(x);
                                }
                        });
                }

                //public void addToCycle(Cycle cyc, string Id, Func<DateTime?, bool> cond, Action<DateTime?> a)
                //{
                //        var Id2 = new CycleElemId(Id, this);
                //        cyc.Add(new CycleElement(Id2) { cond = cond, action = a });
                //        //return cyc;

                //}

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Repeat repeat, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { repeat = repeat, action = a });
                        return cyc;

                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Repeat repeat, params Action<DateTime?>[] actions)
                {
                        return cyc.addToCycle(Id, repeat, x =>
                        {
                                foreach (var action in actions)
                                    action(x);
                        });
                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id,  Importance i, Repeat repeat, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { repeat = repeat, action = a , IsImportant = i == Importance.Important});
                        return cyc;

                }
                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { repeat = repeat, cond = cond, action = a });
                        return cyc;

                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Repeat repeat, Func<DateTime?, bool> cond, params Action<DateTime?>[] actions)
                {
                        return cyc.addToCycle(Id, repeat, cond, x =>
                        {
                                foreach (var action in actions)
                                    action(x);
                        });
                }

                public static Cycle addToCycle(this Cycle cyc, CycleElemId Id, Importance importance, Repeat repeat, Func<DateTime?, bool> cond, Action<DateTime?> a)
                {
                        cyc.Add(new CycleElement(Id) { repeat = repeat, cond = cond, action = a , IsImportant = importance == Importance.Important});
                        return cyc;

                }
















                public static bool notSeenRecentlyHours(this DateTime? lastTime, double ore)
                {
                        if (lastTime == null)
                        {
                                return true;
                        }
                        else
                        {
                                return DateTime.Now.Subtract(lastTime.Value).TotalHours > ore;
                        }
                }

                
                public static bool notSeenRecently(this DateTime? lastTime, double minutes)
                {
                        if (lastTime == null)
                        {
                                return true;
                        }
                        else
                        {
                                return DateTime.Now.Subtract(lastTime.Value).TotalMinutes > minutes;
                        }
                }

                public static bool notSeenRecently(this DateTime lastTime, double minutes, bool onlyIfItHappenedAtLeastOnce = false)
                {
                        if (lastTime == default(DateTime))
                        {
                                if (onlyIfItHappenedAtLeastOnce)
                                {
                                        return false;
                                }
                                return true;
                        }
                        else
                        {
                                return DateTime.Now.Subtract(lastTime).TotalMinutes > minutes;
                        }
                }

                public static bool notSeenRecentlyHours(this DateTime lastTime, double hours, bool onlyIfItHappenedAtLeastOnce = false)
                {
                        if (lastTime == default(DateTime))
                        {
                                if (onlyIfItHappenedAtLeastOnce)
                                {
                                        return false;
                                }
                                return true;
                        }
                        else
                        {
                                return DateTime.Now.Subtract(lastTime).TotalHours > hours;
                        }
                }

                public static string aggregateStringList(this IEnumerable<string> l, string sep = null)
                {
                        if (sep == null)
                        {
                                sep = ", ";
                        }
                        if (l.isEmpty())
                        {
                                return "";
                        }
                        else
                        {
                                return l.Aggregate((x, y) => $"{x}{sep}{y}");
                        }
                }

                public static IEnumerable<X> Flatten<X>(this IEnumerable<IEnumerable<X>> l)
                {
                        return l.SelectMany(x => x);
                }
                //public static List<X> toList<X>(this X el)
                //{
                //        return new List<X> { el };
                //}


                public static List<AnnotatoConIndice<X>> add_indices<X>(this IEnumerable<X> l)
                {
                        return l.Select((el, i) => new AnnotatoConIndice<X> { el = el, i = i }).ToList();
                }


                public static string translatable(this string s)
                {
                        return s;
                }

                public static X itemOrDefault<X,Y>(this Dictionary<Y, X> dic, Y key)
                {
                        if (dic.ContainsKey(key))
                        {
                                return dic[key];
                        }
                        else
                        {
                                return default(X);
                        }
                }


                public static List<X> itemOrEmpty<X, Y>(this Dictionary<Y, List<X>> dic, Y key)
                {
                        if (dic.ContainsKey(key))
                        {
                                return dic[key];
                        }
                        else
                        {
                                return new List<X>();
                        }
                }


        }


        public class StaticUtil
        {
                public static Z calc<Z>(Func<Z> f) { return f(); }
        }
}


