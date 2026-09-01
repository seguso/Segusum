using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace TextAdvEngine
{
    public class pickable
    {

        public pickable()
        {
            //this.lo = lo;
            
        }

        //public string haiRaccoltoIlSecchio;
        //public string haiPoggiatoIlSecchio;


        //public bool prefersToBeDroppedOnGround = false;

        //public bool oggettoAppariscente = false;


        //public Func<Task<bool>> beforePick; 

        ///// <summary>
        ///// leave this to null if it is not a striking object (appariscente)
        ///// </summary>
        //public string strXIsCarryingYHyper2;

        ///// <summary>
        ///// leave this to null if it is not a striking object (appariscente)
        ///// </summary>
        //public parHtmlServer strYouAreCarryingThisHyper;

        //public Action<characterE, characterE, bool, List<cutSceneToken>> cutSceneWhenTheySeeYouCarryingThis;

        
        

        
        
        ///// <summary>
        ///// così come va nell'inv. Singolare, con articolo indeterminativo. Quindi "un mucchio di bottiglie vuote" o "un cacciavite" o "la valigia di Jack".
        ///// </summary>
        //public string descrWithArticleSubjIndSing;

        
        //public logicObjE lo;

        
        //public List<cutSceneToken> dropInAppropriatePlace(roomE r)
        //{
        //    var cs = Utils.cs;
        //    var w = r.wo;

        //    if (r.roomRepresentsContainer is roomRepresentsContainerYes)
        //    {

        //        "Poggi {1}.".tr().inst(this.lo.complOgg(w.ac, det: true) ).tonar().add(cs); // todo migliora scritta: sul pavimento o sul prato
        //        // droppo tranquillamente sul pavimento, tanto in realtà è un cassetto
        //        lo.dropOnFloorLowLevel(r);
        //    }
        //    else
        //    {

        //        if (this.prefersToBeDroppedOnGround && r.parentRoom == null) // se l'oggetto andrebbe per terra, e un terreno esiste (perché non sei una vista di un'altra stanza)
        //        {
        //            "Poggi {1}.".tr().inst(this.lo.complOgg(w.ac, det:true)).tonar().add(cs); // todo migliora scritta: sul pavimento o sul prato
        //            lo.dropOnFloorLowLevel(r);
        //        }
        //        else
        //        {
        //            var rr = (roomRepresentsContainerNo) r.roomRepresentsContainer;

        //            if (rr.whereDoYouDrop != null)
        //            {
        //                // droppo sul tavolino di default della stanza
        //                "Poggi {1} {2}.".tr().inst(this.lo.complOgg(w.ac, det: true)).inst(rr.whereDoYouDrop.complInBucket).tonar().add(cs);
        //                eng.putObjectInContainerMultiple(this.lo, rr.whereDoYouDrop);
        //            }
        //            else
        //            {
        //                // c'è solo il pavimento. se è una stanza figlio, allora non puoi
        //                if (r.parentRoom != null)
        //                {
        //                    // è una stanza figlio che non rappresenta un contenitore. allora non puoi lasciare roba per terra
        //                    "Qui non c'è un posto dove poggiare {1}.".tr().inst(this.lo.complOgg(w.ac, det: true)).tonar().add(cs);
        //                }
        //                else
        //                {
        //                    // è una stanza reale che non rappresenta un contenitore. allora forse puoi lasciare roba sul pavimento, se ha senso.
        //                    if (this.prefersToBeDroppedOnGround)
        //                    {
        //                        "Poggi {1}.".tr().inst(this.lo.complOgg(w.ac, det: true)).tonar().add(cs); // todo migliora scritta: sul pavimento o sul prato
        //                        lo.dropOnFloorLowLevel(r);
        //                    }
        //                    else
        //                    {
        //                        "Qui non c'è un posto adatto per poggiare {1}.".tr().inst(this.lo.complOgg(w.ac, det: true)).tonar().add(cs);
        //                    }

        //                }
        //            }
        //        }
        //    }
        //    return cs;
        //}


        //public void addPickHandler(logicObjE o)
        //{



        //    o.addUnaryHandler(pickUp.i,  (i) =>
        //    {
                

        //        bool suc = true;
        //        //if (beforePick != null)
        //        //    suc =  beforePick();

        //        if (suc)
        //        {
        //            var s = "Hai raccolto {1}.".tr().inst(o.complOgg(o.wo.ac, det: true));


        //            eng.pickUpObjectLowLevel(o.wo.ac, this);


        //            s.tonar().add(i.cs);
        //        }

                
        //    });
        //}

        //public void addDropHandler(logicObjE o)
        //{
        //    // todo vedi se metterlo su uno scaffale o tavolo!

        //    o.addUnaryHandler(drop.i,  (i) =>
        //    {
                
        //        i.cs.AddRange(dropInAppropriatePlace(o.wo.curRoom));
                
        //    });


        //}

        //public void addPutOnHandler(logicObjE o)
        //{

        //    var w = o.wo;

        //    o.addBinaryHandler(putOn.i, (lo2, cs) =>
        //    {

        //        var container = lo2.containers.FirstOrDefault(); // todo dovrei prendere quello che supporta "on"? o quello che accetta questo oggetto? 

        //        if(container != null)
        //        {
        //            if (container.acceptsThisObj == null || container.acceptsThisObj(this))
        //            {

        //                eng.putObjectInContainerMultiple(this, container);
        //                "Metti {1} {2}.".tr().inst(lo.complOgg(w.ac)).inst(container.complInBucket).tonar().add(cs);
        //            }
        //            else
        //            {
        //                "Pensi sia inutile mettere {1} {2}.".tr().inst(lo.complOgg(w.ac)).inst(container.complInBucket).tonar().add(cs);
        //            }
        //        }
                
        //        else
        //        {
        //            "Non vedi che senso abbia.".tr().tonar().add(cs);
        //        }
                

                
        //    });




        //}


    }
}
