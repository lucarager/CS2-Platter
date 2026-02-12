// <copyright file="TestUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests {
    #region Using Statements

    using Colossal.IO.AssetDatabase;
    using Game.Assets;
    using Game.SceneFlow;
    using Game.UI.Debug;
    using Unity.Entities;
    using Hash128 = Colossal.Hash128;

    #endregion

    public static class TestUtils {
        public static bool GetSave(string id, out SaveGameMetadata saveGameMetadata) {
            saveGameMetadata = AssetDatabase.global.GetAsset<SaveGameMetadata>(Hash128.Parse(id));
            return saveGameMetadata != null;
        }

        public static void SetDefaultTestConditions() {
            PlatterMod.Instance.IsTestMode = true;

            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DebugUISystem>().Hide();

            var instance = GameManager.instance;

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