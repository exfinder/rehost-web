//------------------------------------------------------------------------------
// <copyright file="counter.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace System.Web.Util {
    using System;
    using System.Web;
    using System.Runtime.InteropServices;


    /// <devdoc>
    ///    <para>Provides access to system timers.</para>
    /// </devdoc>
    internal sealed class Counter {           

        /// <devdoc>
        ///     not creatable
        /// </devdoc>
        private Counter() {
        }


        /// <devdoc>
        ///    Gets the current system counter value.
        /// </devdoc>
        internal static long Value {
            get {
#if NETFRAMEWORK
                long count = 0;
                SafeNativeMethods.QueryPerformanceCounter(ref count);
                return count;
#else
                return System.Diagnostics.Stopwatch.GetTimestamp();
#endif
            }
        }


        /// <devdoc>
        ///    Gets the frequency of the system counter in counts per second.
        /// </devdoc>
        internal static long Frequency {
            get {
#if NETFRAMEWORK
                long freq = 0;
                SafeNativeMethods.QueryPerformanceFrequency(ref freq);
                return freq;
#else
                // Stopwatch is QueryPerformanceCounter on Windows; the only caller divides one by
                // the other, so the unit matters only for consistency between them.
                return System.Diagnostics.Stopwatch.Frequency;
#endif
            }
        }
    }
}
