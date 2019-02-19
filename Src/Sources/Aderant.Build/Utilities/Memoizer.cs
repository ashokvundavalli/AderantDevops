using System;
using System.Collections.Generic;
using System.Threading;

namespace Aderant.Build.Utilities {

    internal sealed class Memoizer<TArg> : Memoizer<TArg, bool> {
        internal Memoizer(Func<TArg, bool> function, IEqualityComparer<TArg> argComparer = null)
            : base(function, argComparer) {
        }

        public static Memoizer<TArg> True { get; } = new Memoizer<TArg>(arg => true);
        public static Memoizer<TArg> False { get; } = new Memoizer<TArg>(arg => false);
    }

    /// <summary>
    /// Remembers the result of evaluating an expensive function so that subsequent
    /// evaluations are faster. Thread-safe.
    /// </summary>
    /// <typeparam name="TArg"> Type of the argument to the function. </typeparam>
    /// <typeparam name="TResult"> Type of the function result. </typeparam>
    internal class Memoizer<TArg, TResult> {
        private readonly Func<TArg, TResult> function;
        private readonly ReaderWriterLockSlim @lock;
        private readonly Dictionary<TArg, Result> resultCache;

        /// <summary>
        /// Constructs
        /// </summary>
        /// <param name="function"> Required. Function whose values are being cached. </param>
        /// <param name="argComparer"> Optional. Comparer used to determine if two functions arguments are the same. </param>
        internal Memoizer(Func<TArg, TResult> function, IEqualityComparer<TArg> argComparer = null) {
            this.function = function;

            if (argComparer == null) {
                argComparer = EqualityComparer<TArg>.Default;
            }

            resultCache = new Dictionary<TArg, Result>(argComparer);
            @lock = new ReaderWriterLockSlim();
        }


        // <summary>
        // Evaluates the wrapped function for the given argument. If the function has already
        // been evaluated for the given argument, returns cached value. Otherwise, the value
        // is computed and returned.
        // </summary>
        // <param name="arg"> Function argument. </param>
        // <returns> Function result. </returns>
        internal TResult Evaluate(TArg arg) {
            Result result;

            // Check to see if a result has already been computed
            if (!TryGetResult(arg, out result)) {
                // compute the new value
                @lock.EnterWriteLock();
                try {
                    // see if the value has been computed in the interim
                    if (!resultCache.TryGetValue(arg, out result)) {
                        result = new Result(() => function(arg));
                        resultCache.Add(arg, result);
                    }
                } finally {
                    @lock.ExitWriteLock();
                }
            }

            // note: you need to release the global cache lock before (potentially) acquiring
            // a result lock in result.GetValue()
            return result.GetValue();
        }

        private bool TryGetResult(TArg arg, out Result result) {
            @lock.EnterReadLock();
            try {
                return resultCache.TryGetValue(arg, out result);
            } finally {
                @lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Encapsulates a 'deferred' result. The result is constructed with a delegate (must not
        /// be null) and when the user requests a value the delegate is invoked and stored.
        /// </summary>
        private class Result {
            private Func<TResult> func;
            private TResult value;

            internal Result(Func<TResult> createValueFunc) {
                func = createValueFunc;
            }

            internal TResult GetValue() {
                if (null == func) {
                    // if the delegate has been cleared, it means we have already computed the value
                    return value;
                }

                // lock the entry while computing the value so that two threads
                // don't simultaneously do the work
                lock (this) {
                    if (func == null) {
                        // between our initial check and our acquisition of the lock, some other
                        // thread may have computed the value
                        return value;
                    }

                    value = func();

                    // ensure _delegate (and its closure) is garbage collected, and set to null
                    // to indicate that the value has been computed
                    func = null;
                    return value;
                }
            }
        }
    }
}