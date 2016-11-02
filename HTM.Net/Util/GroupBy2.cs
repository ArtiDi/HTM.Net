﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Solvers;

namespace HTM.Net.Util
{

    /**
        * An Java extension to groupby in Python's itertools. Allows to walk across n sorted lists
        * with respect to their key functions and yields a {@link Tuple} of n lists of the
        * members of the next *smallest* group.
        * 
        * @author cogmission
        * @param <R>   The return type of the user-provided {@link Function}s
    */
    //public class GroupBy2<R extends Comparable<R>> implements Generator<Tuple> {
    public class GroupBy2<R> : Generator<Tuple>
            where R : IComparable<R>
    {

        /** serial version */
        private const long serialVersionUID = 1L;

        /** stores the user inputted pairs */
        private Tuple<ICollection, Func<object, R>>[] entries;

        /** stores the {@link GroupBy} {@link Generator}s created from the supplied lists */
        private List<GroupBy<object, R>> generatorList;

        /** the current interation's minimum key value */
        private R minKeyVal;


        ///////////////////////
        //    Control Lists  //
        ///////////////////////
        private bool[] advanceList;
        private Slot<Tuple<object, R>>[] nextList;

        private int numEntries;

        /**
         * Private internally used constructor. To instantiate objects of this
         * class, please see the static factory method {@link #of(Pair...)}
         * 
         * @param entries   a {@link Pair} of lists and their key-producing functions
         */

        private GroupBy2(Tuple<ICollection, Func<object, R>>[] entries)
        {
            this.entries = entries;
        }

        /**
         * <p>
         * Returns a {@code GroupBy2} instance which is used to group lists of objects
         * in ascending order using keys supplied by their associated {@link Function}s.
         * </p><p>
         * <b>Here is an example of the usage and output of this object: (Taken from {@link GroupBy2Test})</b>
         * </p>
         * <pre>
         *  List<Integer> sequence0 = Arrays.asList(new Integer[] { 7, 12, 16 });
         *  List<Integer> sequence1 = Arrays.asList(new Integer[] { 3, 4, 5 });
         *  
         *  Function<Integer, Integer> identity = Function.identity();
         *  Function<Integer, Integer> times3 = x -> x * 3;
         *  
         *  @SuppressWarnings({ "unchecked", "rawtypes" })
         *  GroupBy2<Integer> groupby2 = GroupBy2.of(
         *      new Pair(sequence0, identity), 
         *      new Pair(sequence1, times3));
         *  
         *  for(Tuple tuple : groupby2) {
         *      System.out.println(tuple);
         *  }
         * </pre>
         * <br>
         * <b>Will output the following {@link Tuple}s:</b>
         * <pre>
         *  '7':'[7]':'[NONE]'
         *  '9':'[NONE]':'[3]'
         *  '12':'[12]':'[4]'
         *  '15':'[NONE]':'[5]'
         *  '16':'[16]':'[NONE]'
         *  
         *  From row 1 of the output:
         *  Where '7' == Tuple.get(0), 'List[7]' == Tuple.get(1), 'List[NONE]' == Tuple.get(2) == empty list with no members
         * </pre>
         * 
         * <b>Note: Read up on groupby here:</b><br>
         *   https://docs.python.org/dev/library/itertools.html#itertools.groupby
         * <p> 
         * @param entries
         * @return  a n + 1 dimensional tuple, where the first element is the
         *          key of the group and the other n entries are lists of
         *          objects that are a member of the current group that is being
         *          iterated over in the nth list passed in. Note that this
         *          is a generator and a n+1 dimensional tuple is yielded for
         *          every group. If a list has no members in the current
         *          group, {@link Slot#NONE} is returned in place of a generator.
         */
        //@SuppressWarnings("unchecked")
        //<R extends Comparable<R>>
        public static GroupBy2<R> Of(params Tuple<ICollection, Func<object, R>>[] entries)
        {
            return new GroupBy2<R>(entries);
        }

        /**
         * (Re)initializes the internal {@link Generator}(s). This method
         * may be used to "restart" the internal {@link Iterator}s and
         * reuse this object.
         */
        //@SuppressWarnings("unchecked")
        public override void Reset()
        {
            generatorList = new List<GroupBy<object, R>>();

            for (int i = 0; i < entries.Length; i++)
            {
                generatorList.Add(GroupBy<object, R>.Of(entries[i].Item1, entries[i].Item2));
            }

            numEntries = generatorList.Count;

            //        for(int i = 0;i < numEntries;i++) {
            //            for(Pair<Object, R> p : generatorList.get(i)) {
            //                System.out.println("generator " + i + ": " + p.getKey() + ",  " + p.getValue());
            //            }
            //            System.out.println("");
            //        }
            //        
            //        generatorList = new ArrayList<>();
            //        
            //        for(int i = 0;i < entries.length;i++) {
            //            generatorList.add(GroupBy.of(entries[i].getKey(), entries[i].getValue()));
            //        }

            advanceList = new bool[numEntries];
            Arrays.Fill(advanceList, true);
            nextList = new Slot<Tuple<object, R>>[numEntries];
            Arrays.Fill(nextList, Slot<Tuple<Object, R>>.NONE);
        }

        #region Overrides of Generator<Tuple>

        public override bool MoveNext()
        {
            if (HasNext())
            {
                Current = Next();
                return true;
            }
            return false;
        }

        #endregion

        /**
         * Returns a flag indicating that at least one {@link Generator} has
         * a matching key for the current "smallest" key generated.
         * 
         * @return a flag indicating that at least one {@link Generator} has
         * a matching key for the current "smallest" key generated.
         */
        private bool HasNext()
        {
            if (generatorList == null)
            {
                Reset();
            }

            AdvanceSequences();

            return NextMinKey();
        }

        /**
         * Returns a {@link Tuple} containing the current key in the
         * zero'th slot, and a list objects which are members of the
         * group specified by that key.
         * 
         * {@inheritDoc}
         */
        //@SuppressWarnings("unchecked")
        //@Override
        private Tuple Next()
        {
            object[] objs = ArrayUtils
                .Range(0, numEntries + 1)
                .Select(i => i == 0 ? (object)minKeyVal : new List<R>())
                .ToArray();

            Tuple retVal = new Tuple(objs);

            for (int i = 0; i < numEntries; i++)
            {
                if (IsEligibleList(i, minKeyVal))
                {
                    ((IList)retVal.Get(i + 1)).Add(nextList[i].Get().Item1);
                    DrainKey(retVal, i, minKeyVal);
                    advanceList[i] = true;
                }
                else
                {
                    advanceList[i] = false;
                    ((IList)retVal.Get(i + 1)).Add(/*Slot<Tuple<object, R>>.Empty()*/null);
                }
            }

            return retVal;
        }

        /**
         * Internal method which advances index of the current
         * {@link GroupBy}s for each group present.
         */

        private void AdvanceSequences()
        {
            for (int i = 0; i < numEntries; i++)
            {
                if (advanceList[i])
                {
                    var x = generatorList[i].MoveNext() ? Slot<Tuple<object, R>>.Of(generatorList[i].Current) : Slot<Tuple<object, R>>.Empty();
                    nextList[i] = x;
                }
            }
        }

        /**
         * Returns the next smallest generated key.
         * 
         * @return  the next smallest generated key.
         */

        private bool NextMinKey()
        {
            var nl = nextList
                .Where(opt => opt.IsPresent())
                .Select(opt => opt.Get().Item2)
                .ToList();
            nl.Sort((k, k2) => k.CompareTo(k2));

            R selection = nl.Any() ? nl.First() : default(R);
            if (!selection.Equals(default(R)))
            {
                minKeyVal = selection;
                return true;
            }
            return false;
            //return nextList
            //    .Where(opt=>opt.IsPresent())
            //    .Select(opt=>opt.Get().Item2)
            //    .Min((k, k2) => k.CompareTo(k2))
            //    .Select(k=>
            //{
            //    minKeyVal = k;
            //    return k;
            //})
            //    .isPresent();
        }

        /**
         * Returns a flag indicating whether the list currently pointed
         * to by the specified index contains a key which matches the
         * specified "targetKey".
         * 
         * @param listIdx       the index pointing to the {@link GroupBy} being
         *                      processed.
         * @param targetKey     the specified key to match.
         * @return  true if so, false if not
         */

        private bool IsEligibleList(int listIdx, object targetKey)
        {
            return nextList[listIdx].IsPresent() && nextList[listIdx].Get().Item2.Equals(targetKey);
        }

        /**
         * Each input grouper may generate multiple members which match the
         * specified "targetVal". This method guarantees that all members 
         * are added to the list residing at the specified Tuple index.
         * 
         * @param retVal        the Tuple being added to
         * @param listIdx       the index specifying the list within the 
         *                      tuple which will have members added to it
         * @param targetVal     the value to match in order to be an added member
         */
        //@SuppressWarnings("unchecked")
        private void DrainKey(Tuple retVal, int listIdx, R targetVal)
        {
            while (generatorList[listIdx].MoveNext())
            {
                if (generatorList[listIdx].Peek().Item2.Equals(targetVal))
                {
                    nextList[listIdx] = Slot<Tuple<Object, R>>.Of(generatorList[listIdx].Current);
                    ((IList)retVal.Get(listIdx + 1)).Add(nextList[listIdx].Get().Item1);
                }
                else
                {
                    nextList[listIdx] = Slot<Tuple<Object, R>>.Empty();
                    break;
                }
            }
        }

        /**
         * A minimal {@link Serializable} version of an {@link Slot}
         * @param <T>   the value held within this {@code Slot}
         */

        public sealed class Slot<T> //implements Serializable
        {
            /** Default Serial */
            private const long serialVersionUID = 1L;

            /**
             * Common instance for {@code empty()}.
             */
            public static readonly Slot<T> NONE = new Slot<T>();

            /**
             * If non-null, the value; if null, indicates no value is present
             */
            private readonly T value;

            private Slot()
            {
                this.value = default(T);
            }

            /**
             * Constructs an instance with the value present.
             *
             * @param value the non-null value to be present
             * @throws NullPointerException if value is null
             */

            private Slot(T value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                this.value = value;
            }

            /**
             * Returns an {@code Slot} with the specified present non-null value.
             *
             * @param <T> the class of the value
             * @param value the value to be present, which must be non-null
             * @return an {@code Slot} with the value present
             * @throws NullPointerException if value is null
             */
            public static Slot<T> Of(T value)
            {
                return new Slot<T>(value);
            }

            /**
             * Returns an {@code Slot} describing the specified value, if non-null,
             * otherwise returns an empty {@code Slot}.
             *
             * @param <T> the class of the value
             * @param value the possibly-null value to describe
             * @return an {@code Slot} with a present value if the specified value
             * is non-null, otherwise an empty {@code Slot}
             */
            //@SuppressWarnings("unchecked")
            public static Slot<T> OfNullable(T value)
            {
                return value == null ? (Slot<T>)NONE : Of(value);
            }

            /**
             * If a value is present in this {@code Slot}, returns the value,
             * otherwise throws {@code NoSuchElementException}.
             *
             * @return the non-null value held by this {@code Slot}
             * @throws NoSuchElementException if there is no value present
             *
             * @see Slot#isPresent()
             */

            public T Get()
            {
                if (value == null)
                {
                    throw new IndexOutOfRangeException("No value present");
                }
                return value;
            }

            /**
             * Returns an empty {@code Slot} instance.  No value is present for this
             * Slot.
             *
             * @param <T> Type of the non-existent value
             * @return an empty {@code Slot}
             */

            public static Slot<T> Empty()
            {
                // @SuppressWarnings("unchecked")
                Slot<T> t = (Slot<T>)NONE;
                return t;
            }

            /**
             * Return {@code true} if there is a value present, otherwise {@code false}.
             *
             * @return {@code true} if there is a value present, otherwise {@code false}
             */

            public bool IsPresent()
            {
                return value != null;
            }

            /**
             * Indicates whether some other object is "equal to" this Slot. The
             * other object is considered equal if:
             * <ul>
             * <li>it is also an {@code Slot} and;
             * <li>both instances have no value present or;
             * <li>the present values are "equal to" each other via {@code equals()}.
             * </ul>
             *
             * @param obj an object to be tested for equality
             * @return {code true} if the other object is "equal to" this object
             * otherwise {@code false}
             */

            public override bool Equals(Object obj)
            {
                if (this == obj)
                {
                    return true;
                }

                if (!(obj is Slot<T>))
                {
                    return false;
                }

                Slot<T> other = (Slot<T>)obj;
                return ReferenceEquals(value, other.value);
            }

            /**
             * Returns the hash code value of the present value, if any, or 0 (zero) if
             * no value is present.
             *
             * @return hash code value of the present value or 0 if no value is present
             */

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            /**
             * Returns a non-empty string representation of this Slot suitable for
             * debugging. The exact presentation format is unspecified and may vary
             * between implementations and versions.
             *
             * @implSpec If a value is present the result must include its string
             * representation in the result. Empty and present Slots must be
             * unambiguously differentiable.
             *
             * @return the string representation of this instance
             */

            public override string ToString()
            {
                return value != null ? $"Slot[{value}]" : "NONE";
            }
        }
    }
}