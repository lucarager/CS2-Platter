// <copyright file="TestRunner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
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
        private int  describeCounter;
        private int  itCounter;
        private ILog m_Log;

        public TestRunner(ILog log) {
            m_Log           = LogManager.GetLogger(PlatterMod.ModName + "Tests");
            describeCounter = 0;
            itCounter       = 0;
        }

        public void Describe(string describe, Action action) {
            describeCounter++;
            itCounter = 0;
            m_Log.Info($"{describeCounter}. {describe}");
            action();
        }

        public async Task Describe(string describe, Func<Task> action) {
            describeCounter++;
            itCounter = 0;
            m_Log.Info($"{describeCounter}. {describe}");
            await action();
        }

        public void It(string title, Action action) {
            itCounter++;
            try {
                action();
                m_Log.Info($" {describeCounter}.{itCounter} [PASSED] {title}");
            } catch (Exception e) {
                m_Log.Info($" {describeCounter}.{itCounter} [FAILED] {title}");
                m_Log.Info(e.Message);
            }
        }

        public async Task It(string title, Func<Task> action) {
            itCounter++;
            try {
                await action();
                m_Log.Info($" {describeCounter}.{itCounter} [PASSED] {title}");
            } catch (Exception e) {
                m_Log.Info($" {describeCounter}.{itCounter} [FAILED] {title}");
                m_Log.Info(e.Message);
            }
        }
    }
}