// <copyright file="BurstLogger.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using System.Diagnostics;

    #endregion

    /// <summary>
    /// Provides Burst-compatible logging that is automatically stripped in Burst-compiled code.
    /// Debug messages are only active when USE_BURST is not defined, allowing full job optimization
    /// in Release builds while maintaining logging capability during development.
    /// </summary>
    internal static class BurstLogger {
        /// <summary>
        /// Logs a debug message with a specified context prefix.
        /// This method is automatically compiled out when USE_BURST is defined, ensuring
        /// Burst compatibility. The Conditional attribute also removes the call site in Release builds.
        /// </summary>
        /// <param name="context">The context or system name to prefix the message with (e.g., "RCS").</param>
        /// <param name="message">The message to log.</param>
        [Conditional("DEBUG")]
        public static void Debug(string context, string message) {
#if !USE_BURST
            PlatterMod.Instance.Log.Debug($"[{context}] {message}");
#endif
        }

        /// <summary>
        /// Logs an info message with a specified context prefix.
        /// This method is automatically compiled out when USE_BURST is defined, ensuring
        /// Burst compatibility.
        /// </summary>
        /// <param name="context">The context or system name to prefix the message with.</param>
        /// <param name="message">The message to log.</param>
        [Conditional("DEBUG")]
        public static void Info(string context, string message) {
#if !USE_BURST
            PlatterMod.Instance.Log.Info($"[{context}] {message}");
#endif
        }

        /// <summary>
        /// Logs a warning message with a specified context prefix.
        /// This method is automatically compiled out when USE_BURST is defined, ensuring
        /// Burst compatibility.
        /// </summary>
        /// <param name="context">The context or system name to prefix the message with.</param>
        /// <param name="message">The message to log.</param>
        [Conditional("DEBUG")]
        public static void Warn(string context, string message) {
#if !USE_BURST
            PlatterMod.Instance.Log.Warn($"[{context}] {message}");
#endif
        }

        /// <summary>
        /// Logs an error message with a specified context prefix.
        /// This method is automatically compiled out when USE_BURST is defined, ensuring
        /// Burst compatibility.
        /// </summary>
        /// <param name="context">The context or system name to prefix the message with.</param>
        /// <param name="message">The message to log.</param>
        [Conditional("DEBUG")]
        public static void Error(string context, string message) {
#if !USE_BURST
            PlatterMod.Instance.Log.Error($"[{context}] {message}");
#endif
        }
    }
}
