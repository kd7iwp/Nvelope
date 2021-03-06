﻿using System;
using System.Collections.Generic;
using System.Linq;
using Nvelope.Collections;

namespace Nvelope
{
    public static class FunctionExtensions
    {
        /// <summary>
        /// Return a new function that will cache the results of the function, so subsequent invocations don't
        /// have to re-execute the original function
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<TResult> Memoize<TResult>(this Func<TResult> func)
        {
            bool hasVal = false;
            TResult val = default(TResult);
            return () =>
                {
                    if (!hasVal)
                    {
                        val = func();
                        hasVal = true;
                    }
                    return val;
                };
        }

        /// <summary>
        /// Return a new function that will cache the results of each call, so subsequent invocations don't
        /// have to re-execute the original function
        /// </summary>
        public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func)
        {
            Dictionary<T, TResult> results = new Dictionary<T, TResult>();
            return t =>
                {
                    if (!results.ContainsKey(t))
                        results.Add(t, func(t));
                    return results[t];
                };
        }

        /// <summary>
        /// Return a new function that will cache the results of each call, so subsequent invocations don't
        /// have to re-execute the original function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<T, U, TResult> Memoize<T, U, TResult>(this Func<T, U, TResult> func)
        {
            Dictionary<T, Dictionary<U, TResult>> results = new Dictionary<T, Dictionary<U, TResult>>();
            return (t, u) =>
                {
                    if (!results.ContainsKey(t))
                        results.Add(t, new Dictionary<U, TResult>());
                    if (!results[t].ContainsKey(u))
                        results[t].Add(u, func(t, u));
                    return results[t][u];
                };
        }

        /// <summary>
        /// Return a new function that will cache the results of each call, so subsequent invocations don't
        /// have to re-execute the original function
        /// </summary>
        /// <param name="useCacheFn">This function controls whether to use the cached value or not. 
        /// T means use the cached value, F means reload it</param>
        public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func, Func<T, bool> useCacheFn)
        {
            Dictionary<T, TResult> results = new Dictionary<T, TResult>();
            return t =>
                {
                    // We need to make sure we call the useCacheFn every time, since 
                    // clients (ie, Memoize(TimeSpan) might rely on it being invoked every time.
                    var useCache = useCacheFn(t);

                    if(!results.ContainsKey(t))
                        results.Add(t, func(t));
                    else if(!useCache)
                        results[t] = func(t);

                    return results[t];
                };
        }

        /// <summary>
        /// Cache the results of the function for the specified duration. The
        /// results are cached on a per-argument basis, so f(x) expires at a
        /// different time than f(y)
        /// </summary>
        /// <param name="duration">How long to wait before dropping something
        /// out of the cache</param>
        public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func, TimeSpan duration)
        {
            return func.Memoize(HasBeenCalledIn<T>(duration));
        }

        /// <summary>
        /// Used by Memoize to implement TimeSpan caching. Returns a function that returns true if it was
        /// called with a given value since duration ago
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static Func<T, bool> HasBeenCalledIn<T>(TimeSpan duration)
        {
            Dictionary<T,DateTime> cacheDte = new Dictionary<T,DateTime>();
            return t =>
                {
                    // Get either the last time we loaded the cache for the supplied value, or DateTime.MinValue
                    var curVal = cacheDte.Val(t, DateTime.MinValue);
                    // See if the cache has expired.
                    // Note: if the duration is set to some very long value (greater than DateTime.Now.Minus(DateTime.MinValue)
                    // then this function will falsely return true (indicating that the cached value should be used) even
                    // if the cache doesn't contain this value yet. However, this shouldn't be a problem, because Memoize will
                    // check it's own cache internally, recognize that it doesn't have the value, and run the underlying func anyways
                    var isValid = curVal.Add(duration) > DateTime.Now;
                    // If we haven't previously cached the value, or the value has expired,
                    // Set the new cache time to Now, because we'll tell Memoize to rerun the
                    // function and store Now as the last time we loaded the cache
                    if (!isValid)
                        cacheDte.Ensure(t, DateTime.Now);

                    return isValid;
                };
        }

        /// <summary>
        /// Returns a function that returns true if the function has been called n times in the last duration
        /// </summary>
        /// <param name="n"></param>
        /// <param name="duration"></param>
        /// <returns></returns>
        public static Func<bool> HasBeenCalledNTimesIn(int n, TimeSpan duration)
        {
            var queue = new FixedSizeQueue<DateTime>(n);
            return () =>
                {
                    var now = DateTime.Now;
                    var before = now.Subtract(duration);
                    if (queue.Count == n && queue.All(dt => dt > before))
                        return true;

                    queue.Enqueue(now);
                    return false;
                };
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        /// <remarks>Also known as partial application</remarks>
        public static Func<TResult> Curry<TCurry, TResult>
            (this Func<TCurry, TResult> func, TCurry value)
        {
            var val = value;
            return () => func(val);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        /// <remarks>Also known as partial application</remarks>
        public static Func<T, TResult> Curry<TCurry, T, TResult>
            (this Func<TCurry, T, TResult> func, TCurry value)
        {
            var val = value;
            return u => func(val, u);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        public static Func<T1, T2, TResult> Curry<TCurry, T1, T2, TResult>
            (this Func<TCurry, T1, T2, TResult> func, TCurry value)
        {
            var val = value;
            return (u, v) => func(val, u, v);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        public static Func<T1, T2, T3, TResult> Curry<TCurry, T1, T2, T3, TResult>
            (this Func<TCurry, T1, T2, T3, TResult> func, TCurry value)
        {
            var val = value;
            return (u, v, w) => func(val, u, v, w);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        public static Action<TResult> Curry<TCurry, TResult>
            (this Action<TCurry, TResult> func, TCurry value)
        {
            var val = value;
            return u => func(val, u);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        public static Action<T, TResult> Curry<TCurry, T, TResult>
            (this Action<TCurry, T, TResult> func, TCurry value)
        {
            var val = value;
            return (u, v) => func(val, u, v);
        }

        /// <summary>
        /// Transform a function into one taking one less argument by supplying a value
        /// to always use for the first argument
        /// </summary>
        public static Action<T1, T2, TResult> Curry<TCurry, T1, T2, TResult>
            (this Action<TCurry, T1, T2, TResult> func, TCurry value)
        {
            var val = value;
            return (u, v, w) => func(val, u, v, w);
        }

        /// <summary>
        /// Choose one of two functions to run, based on the results of a third function.
        /// If the dispatch function returns true, trueFn will be run and returned; if false,
        /// falseFn will be run and returned
        /// </summary>
        public static Func<T, TResult> Dispatch<T, TResult>(this Func<T, bool> dispatchFn, Func<T, TResult> trueFn, Func<T, TResult> falseFn)
        {
            return t =>
            {
                if (dispatchFn(t))
                    return trueFn(t);
                else
                    return falseFn(t);
            };
        }

        /// <summary>
        /// Reverse the parameters of a function: f(t,u) becomes f(u,t)
        /// </summary>
        public static Func<T2, T1, TResult> ReverseArgs<T1, T2, TResult>(this Func<T1, T2, TResult> func)
        {
            return (u, t) => func(t, u);
        }

        /// <summary>
        /// Reverse the parameters of a function: f(t,u,v) becomes f(v,u,t)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<T3, T2, T1, TResult> ReverseArgs<T1, T2, T3, TResult>(this Func<T1, T2, T3, TResult> func)
        {
            return (v, u, t) => func(t, u, v);
        }

        /// <summary>
        /// Create a new function that modifies its argument before passing it to the 
        /// underlying function
        /// </summary>
        public static Func<T, TResult> FilterArg<T, TResult>(this Func<T, TResult> func, Func<T, T> argFilter)
        {
            return t => func(argFilter(t));
        }

        /// <summary>
        /// This function lets us create a new function from a list of an anonymous type
        /// This is pretty much the only way I can think of to create a variable that holds
        /// a function that uses an anonymous type as one of its params, since you can't actually
        /// literally reference an anonymous type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="list"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<T, TResult> CreateFunc<T, TResult>(this IEnumerable<T> list, Func<T, TResult> func)
        {
            return func;
        }
        
        /// <summary>
        /// Execute some transformation function recursively until some haltCondition returns true.        
        /// </summary>
        /// <remarks>It's not really recursive, but gives you the semantics of recursion</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="haltCondition"></param>
        /// <returns></returns>
        public static Func<T, T> Until<T>(this Func<T, T> func, Func<T, bool> haltCondition)
        {
            return t =>
            {
                T cur = t;
                while(!haltCondition(cur))                                
                    cur = func(cur);                
                return cur;
            };
        }

        /// <summary>
        /// Execute some transformation function recursively until the value stops changing
        /// (ie, returns the same value twice in a row)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Func<T, T> UntilStops<T>(this Func<T, T> func)
        {
            return func.Until(SameAsLast<T>());
        }

        /// <summary>
        /// A function that returns true if the same value was returned twice in a row
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Func<T, bool> SameAsLast<T>()
        {
            T last = default(T);
            bool hasLast = false;
            return t =>
                {
                    var res = hasLast && last.Eq(t);
                    last = t;
                    hasLast = true;
                    return res;
                };
        }
               
        /// <summary>
        /// Returns a new function that executes func, then otherFunc
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="otherFunc"></param>
        /// <returns></returns>
        public static Func<T, T> Then<T>(this Func<T, T> func, Func<T, T> otherFunc)
        {
            return t =>
                {
                    var partA = func(t);
                    var partB = otherFunc(partA);
                    return partB;
                };
        }

        /// <summary>
        /// Returns a new function that is a logical OR of the two source functions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="otherPredicate"></param>
        /// <returns></returns>
        public static Func<T, bool> Or<T>(this Func<T, bool> predicate, Func<T, bool> otherPredicate)
        {
            return t => predicate(t) || otherPredicate(t);
        }

        /// <summary>
        /// Returns a new function that is a logical AND of the two source functions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="otherPredicate"></param>
        /// <returns></returns>
        public static Func<T, bool> And<T>(this Func<T, bool> predicate, Func<T, bool> otherPredicate)
        {
            return t => predicate(t) && otherPredicate(t);
        }

        /// <summary>
        /// Run a function a bunch of times, and return the average execution time
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="fn"></param>
        /// <param name="testData"></param>
        /// <returns></returns>
        public static int Benchmark<T, TResult>(
            this Func<T, TResult> fn,
            IEnumerable<T> testData,
            int numRuns = 5)
        {
            var times = 1.To(numRuns).Select(i =>
            {
                var start = DateTime.Now;
                foreach(var t in testData)
                    fn(t);
                var end = DateTime.Now;
                return end.Subtract(start).Milliseconds;
            });

            return times.Sum() / numRuns;
        }

        /// <summary>
        /// Run an action a bunch of times, and return the average execution time
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="fn"></param>
        /// <param name="testData"></param>
        /// <param name="numRuns"></param>
        /// <returns></returns>
        public static int Benchmark<T1, T2>(
            this Action<T1, T2> fn,
            IEnumerable<KeyValuePair<T1, T2>> testData,
            int numRuns = 5)
        {
            var times = 1.To(numRuns).Select(i => 
            {
                var start = DateTime.Now;
                foreach(var kv in testData)
                    fn(kv.Key, kv.Value);
                var end = DateTime.Now;
                return end.Subtract(start).Milliseconds;
            });

            return times.Sum() / numRuns;
        }
    }
}
