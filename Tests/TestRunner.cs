// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.Logging;

namespace Platter.Tests {
    internal class TestRunner {
        private ILog   m_Log;
        private string m_Describe;
        private string m_It;

        public TestRunner(ILog log) {
            m_Log      = log;
            m_Describe = "";
            m_It       = "";
        }

        public void Describe(string describe, Action action) {
            this.m_Describe = describe;
            m_Log.Info($"{describe}");
            action();
        }

        public void It(string title, Action action) {
            try {
                action();
                m_Log.Info($"   [PASSED] {title}");
            } catch (Exception e) {
                m_Log.Error($"   [FAILED] {title}");
            }
        }
    }
}
