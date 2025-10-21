// <copyright file="VerifyParcelBehaviorTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Colossal.IO.AssetDatabase;
using Colossal.TestFramework;
using Game;
using Game.Assets;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Platter.Components;
using Platter.Tests;
using Platter.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ConnectedBuilding = Game.Buildings.ConnectedBuilding;
using Edge = Game.Net.Edge;

namespace Platter.Tests {
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Colossal.Assertions;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;
    using Game;
    using Game.Assets;
    using Game.Buildings;
    using Game.City;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Simulation;
    using Platter.Components;
    using Platter.Systems;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Transform = Game.Objects.Transform;

    [TestDescriptor("Platter: Verify Parcel Behavior", Category.Serialization, false, TestPhase.Default, false)]
    public class VerifyParcelBehaviorTest : TestScenario {
        private const string MapID = "bf8036b291428535b986c757fda3e627";
        private const string CityName = "TestCity-1";
        private const string Theme = "European";
        private const string Save1 = "b526dc207d32a74363a6cce5d6c8d85a";

        private static Transform ParcelTransform1 = new Transform(new float3(493.8864f, 875.3773f, 497.6312f), new quaternion(0, 0, 0, 1f));

        // Systems
        private P_TestToolSystem m_TestToolSystem;
        private PrefabSystem     m_PrefabSystem;

        // Queries
        private EntityManager m_EM;

        private TestRunner TR;

        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            TR = new TestRunner(log);
            log.Info("OnPrepare");

            TestUtils.SetDefaultTestConditions();
            await Task.CompletedTask;

            m_EM = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Get Systems
            m_TestToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_TestToolSystem>();
            m_PrefabSystem   = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [Test]
        private Task StartNewGameAndTest() {
            return Execute();
        }

        private static bool GetSave(string id, out SaveGameMetadata saveGameMetadata) {
            saveGameMetadata = AssetDatabase.global.GetAsset<SaveGameMetadata>(global::Colossal.Hash128.Parse(id));
            return saveGameMetadata != null;
        }

        private MapMetadata PrepareMap() {
            var existingSystemManaged = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CityConfigurationSystem>();
            existingSystemManaged.overrideLoadedOptions = true;
            existingSystemManaged.overrideCityName = CityName;
            existingSystemManaged.overrideThemeName = Theme;
            existingSystemManaged.overrideLeftHandTraffic = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TimeSystem>().startingYear = DateTime.Now.Year;

            return AssetDatabase.global.GetAsset<MapMetadata>(global::Colossal.Hash128.Parse(MapID));
        }

        private async Task Execute(SaveGameMetadata saveGameMetadata = null) {
            log.Info("Execute");

            var map = PrepareMap();

            if (map == null) {
                log.ErrorFormat("Asset {0} was not found. Test {1} skipped.", MapID, this);
                return;
            }

            // Create the game
            if (saveGameMetadata != null) {
                await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGameMetadata);
            } else {
                await GameManager.instance.Load(GameMode.Game, Purpose.NewGame, map);
            }
            await WaitFrames();

            // Activate tool
            m_TestToolSystem.Enable();

            // Retrieve prefabs
            if (!m_PrefabSystem.TryGetPrefab(new PrefabID("ParcelPrefab", "Parcel 2x2"), out var parcelPrefabBase)) {
                throw new Exception("Parcel prefab not found");
            }

            var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);

            m_TestToolSystem.Place(prefabEntity, ParcelTransform1);

            // # [B] Verify Parcel behavior
            // # ######################################################
            // # 1. New Parcels
            // #      1.1. A new parcel should have correct data after creation
            // #      1.2. A new parcel should have a ParcelSubBlock buffer of size 1.
            // #      1.3. A parcel's Block should exist, have the right components and number of Cells in its buffer
            // # 2. Road Connections
            // #      2.1 Placing a parcel away from roads should show a road connection notification
            // #      2.2 Placing a parcel next to a road should connect the two
            // #      2.3 Placing a road next to an existing parcel should connect the two
            // #          2.3.1 Parcel in road's ConnectedParcel
            // #          2.3.2 Road in parcel's Parcel Data
            // #          2.3.3 Road in parcel's Block's Owner
            // #      2.4 Deleting a road next to an existing parcel should disconnect the two and show a missing road status
            // #      2.5 Deleting a parcel connected to a road should remove it from its ConnectedParcel buffer

            //await GameManager.instance.MainMenu();
        }
    }
}
