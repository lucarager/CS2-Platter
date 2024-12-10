﻿// <copyright file="PrefixedLogger.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using Colossal.Logging;

    internal class PrefixedLogger {
        private readonly string m_Prefix;
        private readonly ILog m_Log;

        public PrefixedLogger(string prefix) {
            m_Prefix = prefix;
            m_Log = PlatterMod.Instance.Log;
        }

        public void Info(string message) {
            Log("INFO", message);
        }

        public void Warn(string message) {
            Log("WARN", message);
        }

        public void Error(string message) {
            Log("ERROR", message);
        }

        public void Debug(string message) {
            Log("DEBUG", message);
        }

        private void Log(string level, string message) {
            string formattedMessage = $"[{m_Prefix}] {message}";

            switch (level) {
                case "ERROR":
                    m_Log.Error(formattedMessage);
                    break;
                case "WARN":
                    m_Log.Warn(formattedMessage);
                    break;
                case "DEBUG":
                    m_Log.Debug(formattedMessage);
                    break;
                case "INFO":
                default:
                    m_Log.Info(formattedMessage);
                    break;
            }
        }
    }
}
