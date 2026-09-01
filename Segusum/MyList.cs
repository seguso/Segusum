using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Seg
{
        public class MyList<A> : IEnumerable<A>
        {



                MyListNode<A> head_node;


                public int count;




                public A head => head_node.el;

                public MyList()
                {
                        head_node = null;
                        count = 0;
                }




                public List<A> ToList()
                {
                        return this.Reverse().ToList();

                }


                public MyList(IEnumerable<A> l)
                {

                        foreach (var x in l.Reverse())
                        {
                                var newNode = new MyListNode<A> { el = x, next = head_node };

                                head_node = newNode;
                                count++;
                        }
                }

                public IEnumerator<A> GetEnumerator()
                {
                        var curNode = this.head_node;

                        while (curNode != null)
                        {


                                yield return curNode.el;

                                curNode = curNode.next;


                        }
                }

                IEnumerator IEnumerable.GetEnumerator()
                {

                        var curNode = this.head_node;

                        while (curNode != null)
                        {


                                yield return curNode.el;

                                curNode = curNode.next;


                        }
                }



                public static MyList<A> Empty

                {
                        get
                        {
                                return new MyList<A> { head_node = null, count = 0 };
                        }
                }


                //List<A> ToListAux(List<A> retSoFar)
                //{
                //    if (head == null)
                //    {
                //        return retSoFar;
                //    }
                //    else
                //    {
                //        retSoFar.Add(head);

                //        return tail.ToListAux(retSoFar);
                //    }


                //}

                //public List<A> ToList()
                //{

                //    var ret = new List<A>();
                //    return ToListAux(ret);
                //}




                public bool Any()
                {
                        return head_node != null;
                }


                public MyList<A> tail
                {
                        get
                        {
                                if (head_node == null)
                                {
                                        throw new Exception("lista vuota");
                                }
                                else
                                {
                                        return new MyList<A> { head_node = head_node.next, count = count - 1 };
                                }
                        }
                }

                public (A, MyList<A>) deconstruct()
                {
                        if (head_node == null)
                        {
                                throw new Exception("lista vuota");
                        }
                        else
                        {
                                return (head_node.el, new MyList<A> { head_node = head_node.next, count = count - 1 });
                        }
                }


                public MyList<A> AddO1(A el)
                {

                        var newHead = new MyListNode<A>
                        {
                                el = el,
                                next = this.head_node
                        };

                        return new MyList<A>
                        {
                                head_node = newHead,
                                count = this.count + 1
                        };

                }


                public MyList<A> Add_in_coda(A el)
                {
                        var thisEnumerable = (IEnumerable<A>)this; // per chiamare il tolist che non fa reverse
                        var l = thisEnumerable.ToList();
                        l.Add(el);
                        return l.ToMyList();

                }

                public int Count => count;


                public MyList<A> RemoveIfEquals(A elToRem)
                {

                        if (this.head_node == null)
                        {
                                return this;
                        }
                        else if (this.head_node.el.Equals(elToRem))
                        {
                                return new MyList<A>
                                {
                                        head_node = head_node.next,
                                        count = count - 1
                                };
                        }
                        else
                        {
                                return this.tail.RemoveIfEquals(elToRem).AddO1(head_node.el);
                        }

                }

                public MyList<A> Remove_range(IEnumerable<A> elsToRem)
                {

                        var tmp = this;
                        foreach (var el in elsToRem)
                        {
                                tmp = tmp.RemoveIfEquals(el);
                        }
                        return tmp;

                }


                public MyList<A> Add_range(IEnumerable<A> elsToAdd)
                {

                        var tmp = this;
                        foreach (var el in elsToAdd)
                        {
                                tmp = tmp.AddO1(el);
                        }
                        return tmp;

                }

                public override string ToString()
                {
                        return this.Select(x => x.ToString()).aggregateStringList(sep: ", \n");
                }
        }
}
