// <copyright file="GameTestUtility.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.Logging;
using Game.SceneFlow;
using Game.Settings;
using Game.UI.Debug;
using Unity.Entities;

namespace Platter.Tests {
    public static class TestUtils {
        public static void SetDefaultTestConditions() {
            return;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DebugUISystem>().Hide();

            var instance = GameManager.instance;
            if (instance != null) {
                instance.settings.Reset();
            }

            instance = GameManager.instance;
            var gameplaySettings = instance == null ? null : instance.settings?.gameplay;

            if (gameplaySettings != null) {
                gameplaySettings.edgeScrolling      = false;
                gameplaySettings.pausedAfterLoading = false;
                gameplaySettings.showTutorials      = false;
                gameplaySettings.Apply();
            }

            instance = GameManager.instance;
            var interfaceSettings = instance == null ? null : instance.settings?.userInterface;

            if (interfaceSettings != null) {
                interfaceSettings.blockingPopupsEnabled = false;
                interfaceSettings.Apply();
            }
        }
    }
}