using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Seg
{

     



        public class CycleElemId 
        {
                //public CycleElemId(string id)
                //{
                //        //wb.DeclaredCycleIds.Add(Id);

                //        Id = id;
                //}

                //public string Id { get; set; }
        }

        public static class CycleMemory
        {

                //public static CycleMemory create_from_cycle(IEnumerable<CycleElement> cycl, WorldBase wb)
                //{
                //        var mem = new CycleMemory
                //        {
                //                IndexOfNextElementToTry = 0,
                //                //howManyTimesElementExecuted = new Dictionary<CycleElemId, int>()

                //        };
                //        //foreach(var el in cycl)
                //        //{
                //        //        mem.howManyTimesElementExecuted.Add(el.Id, 0);
                //        //}
                //        return mem;
                //}

                //public int IndexOfNextElementToTry = 0;

                

                public static bool wouldSaySomethingNew(IEnumerable<CycleElement> cycle, WorldBase wb)
                {
                        foreach (var el in cycle)
                        {
                                //var el = x.el;



                                DateTime? lastTimeExecuted;
                                if (wb.lastTimeElementExecuted.ContainsKey(el.Id))
                                {
                                        lastTimeExecuted = wb.lastTimeElementExecuted[el.Id];
                                }
                                else
                                {
                                        lastTimeExecuted = null;
                                }



                                var condHolds = el.cond(lastTimeExecuted);
                                if (condHolds)
                                {

                                        if (!wb.howManyTimesElementExecuted.ContainsKey(el.Id))
                                        {
                                                // forse ho aggiunto un cycle item a partita iniziata. metti una pezza
                                                wb.howManyTimesElementExecuted[el.Id] = 0;
                                        }

                                        if (wb.howManyTimesElementExecuted[el.Id] == 0)
                                        {

                                                var retryDebug = el.cond(lastTimeExecuted);

                                                return true;
                                        }
                                }
                        }
                        return false;

                }

                public static bool wouldSaySomethingNewAndImportant(IEnumerable<CycleElement> cycle, WorldBase wb, CycleElemId differentFrom = null)
                {
                        foreach (var el in cycle)
                        {
                                //var el = x.el;
                                if (el.Id == differentFrom)
                                {
                                        continue;
                                }


                                DateTime? lastTimeExecuted;
                                if (wb.lastTimeElementExecuted.ContainsKey(el.Id))
                                {
                                        lastTimeExecuted = wb.lastTimeElementExecuted[el.Id];
                                }
                                else
                                {
                                        lastTimeExecuted = null;
                                }



                                var condHolds = el.cond(lastTimeExecuted);
                                if (condHolds)
                                {

                                        // in teoria possiamo dire el, perche' la condizione vale. ora dobbiamo vedere se e' nuovo, e se e' importante

                                        if (!wb.howManyTimesElementExecuted.ContainsKey(el.Id))
                                        {
                                                // forse ho aggiunto un cycle item a partita iniziata. metti una pezza
                                                wb.howManyTimesElementExecuted[el.Id] = 0;
                                        }

                                        if (wb.howManyTimesElementExecuted[el.Id] == 0)
                                        {

                                                // e' nuovo. 

                                                if (el.IsImportant)
                                                {


                                                        //var retryDebug = el.cond(lastTimeExecuted);

                                                        return true;
                                                }
                                                // altrimenti continuiamo al prossimo el
                                        }
                                }
                        }
                        return false;

                }



                public static bool wouldSaySomething(IEnumerable<CycleElement> cycle, WorldBase wb, CycleElemId differentFrom = null)
                {
                        foreach (var el in cycle)
                        {
                                if (el.Id == differentFrom)
                                {
                                        continue;
                                }

                                DateTime? lastTimeExecuted;
                                if (wb.lastTimeElementExecuted.ContainsKey(el.Id))
                                {
                                        lastTimeExecuted = wb.lastTimeElementExecuted[el.Id];
                                }
                                else
                                {
                                        lastTimeExecuted = null;
                                }




                                if (el.cond(lastTimeExecuted))
                                {

                                        if (!wb.howManyTimesElementExecuted.ContainsKey(el.Id))
                                        {
                                                return true; // copio quello sotto
                                        }
                                        if (wb.howManyTimesElementExecuted[el.Id] == 0)
                                        {
                                                return true;
                                        }
                                        else if (el.repeat == Repeat.Forever)
                                        {
                                                return true;
                                        }
                                }
                        }
                        return false;

                }

                //public void serialize(XElement xelParent, string name = null)
                //{
                //        var x = new XElement("cycle_memory");

                //        xelParent.Add(x);


                //        if (name != null)
                //        {
                //                x.Add(new XAttribute("name", name));
                //        }

                //        x.Add(new XAttribute("next_element_to_try", IndexOfNextElementToTry));

                //        //foreach (var q in howManyTimesElementExecuted)
                //        //{
                //        //        var xelq = new XElement("how_many_times_element_executed");
                //        //        x.Add(xelq);

                //        //        xelq.Add(new XAttribute("i_el", q.Key));

                //        //        xelq.Add(new XAttribute("times", q.Value));
                //        //}



                //        //foreach (var q in lastTimeElementExecuted)
                //        //{
                //        //        var xelq = new XElement("last_time_element_exec");
                //        //        x.Add(xelq);

                //        //        xelq.Add(new XAttribute("i_el", q.Key));

                //        //        xelq.Add(new XAttribute("time", q.Value.ToString(CultureInfo.InvariantCulture)));
                //        //}




                //}

                //public string deserialize(XElement xel)
                //{
                //        IndexOfNextElementToTry = int.Parse(xel.Attribute("next_element_to_try")?.Value ?? throw new InvalidOperationException());

                //        //howManyTimesElementExecuted.Clear();
                //        //foreach (var xelh in xel.Elements("how_many_times_element_executed"))
                //        //{

                //        //        var key = int.Parse(xelh.Attribute("i_el")?.Value ?? throw new InvalidOperationException());
                //        //        var val = int.Parse(xelh.Attribute("times")?.Value ?? throw new InvalidOperationException());


                //        //        throw new NotFiniteNumberException();
                //        //        //howManyTimesElementExecuted.Add(key, val);

                //        //}


                //        //lastTimeElementExecuted.Clear();
                //        //foreach (var xelh in xel.Elements("last_time_element_exec"))
                //        //{

                //        //        var key = int.Parse(xelh.Attribute("i_el")?.Value ?? throw new InvalidOperationException());
                //        //        var val = DateTime.Parse(xelh.Attribute("time")?.Value ?? throw new InvalidOperationException(), CultureInfo.InvariantCulture);

                //        //        throw new NotFiniteNumberException();
                //        //        //lastTimeElementExecuted.Add(key, val);

                //        //}


                //        if (xel.Attribute("name") != null)
                //        {
                //                return xel.Attribute("name")?.Value;
                //        }
                //        else
                //        {
                //                return null;
                //        }
                //}
        }
}
