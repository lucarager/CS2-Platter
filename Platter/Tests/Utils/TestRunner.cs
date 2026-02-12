// <copyright file="TestRunner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests {
    #region Using Statements

    using System;
    using System.Threading.Tasks;
    using Colossal.Logging;

    #endregion

    internal class TestRunner {
        private ILog m_Log;
        private int  describeCounter;
        private int  failedTests;
        private int  itCounter;
        private int  passedTests;

        public TestRunner(ILog log) {
            m_Log           = LogManager.GetLogger(PlatterMod.ModName + "Tests");
            describeCounter = 0;
            itCounter       = 0;
            passedTests     = 0;
            failedTests     = 0;
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
                passedTests++;
                m_Log.Info($" {describeCounter}.{itCounter} [PASSED] {title}");
            } catch (Exception e) {
                failedTests++;
                m_Log.Info($" {describeCounter}.{itCounter} [FAILED] {title}");
                m_Log.Info(e.Message);
            }
        }

        public async Task It(string title, Func<Task> action) {
            itCounter++;
            try {
                await action();
                passedTests++;
                m_Log.Info($" {describeCounter}.{itCounter} [PASSED] {title}");
            } catch (Exception e) {
                failedTests++;
                m_Log.Info($" {describeCounter}.{itCounter} [FAILED] {title}");
                m_Log.Info(e.Message);
            }
        }

        public void Complete() {
            PrintSummary();
            ResetCounters();
        }

        public void PrintSummary() {
            var totalTests = passedTests + failedTests;
            m_Log.Info(string.Empty);
            m_Log.Info("====== TEST SUMMARY ======");
            m_Log.Info($"Total Tests: {totalTests}");
            m_Log.Info($"Passed: {passedTests}");
            m_Log.Info($"Failed: {failedTests}");
            m_Log.Info("==========================");
        }

        public void ResetCounters() {
            describeCounter = 0;
            itCounter       = 0;
            passedTests     = 0;
            failedTests     = 0;
        }
    }
}