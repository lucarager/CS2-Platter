// <copyright file="VerifyParcelBehaviorTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Game.Tools;
using Game.Zones;

namespace Platter.Tests {
    using System;
    using System.Threading.Tasks;
    using Colossal.IO.AssetDatabase;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;
    using Game;
    using Game.Assets;
    using Game.City;
    using Game.Input;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Simulation;
    using Systems;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.InputSystem.Users;
    using Transform = Game.Objects.Transform;

    [TestDescriptor("Platter: Verify Parcel Behavior", Category.Serialization)]
    public class VerifyParcelBehaviorTest : TestScenario {
        private const string MapID    = "bf8036b291428535b986c757fda3e627";
        private const string CityName = "TestCity-1";
        private const string Theme    = "European";
        private const string Save1    = "b526dc207d32a74363a6cce5d6c8d85a";

        private static Transform ParcelTransform1 = new(new float3(493.8864f, 875.3773f, 497.6312f), new quaternion(0, 0, 0, 1f));

        // Queries
        private EntityManager m_EM;
        private PrefabSystem  m_PrefabSystem;

        // Systems
        private P_TestToolSystem m_TestToolSystem;
        private ToolSystem       m_ToolSystem; 
        private ObjectToolSystem m_ObjectToolSystem;
        private TestRunner       TR;

        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            TR = new TestRunner(log);
            log.Info("OnPrepare");

            TestUtils.SetDefaultTestConditions();
            await Task.CompletedTask;

            m_EM = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Get Systems
            m_TestToolSystem   = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_TestToolSystem>();
            m_ObjectToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PrefabSystem     = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem       = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [Test]
        private Task StartNewGameAndTest() { return Execute(); }

        private static bool GetSave(string id, out SaveGameMetadata saveGameMetadata) {
            saveGameMetadata = AssetDatabase.global.GetAsset<SaveGameMetadata>(Colossal.Hash128.Parse(id));
            return saveGameMetadata != null;
        }

        private MapMetadata PrepareMap() {
            var existingSystemManaged = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CityConfigurationSystem>();
            existingSystemManaged.overrideLoadedOptions                                               = true;
            existingSystemManaged.overrideCityName                                                    = CityName;
            existingSystemManaged.overrideThemeName                                                   = Theme;
            existingSystemManaged.overrideLeftHandTraffic                                             = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TimeSystem>().startingYear = DateTime.Now.Year;

            return AssetDatabase.global.GetAsset<MapMetadata>(Colossal.Hash128.Parse(MapID));
        }

        private async Task PlaceParcel(PrefabID prefabID, Transform transform) {
            if (!m_PrefabSystem.TryGetPrefab(prefabID, out var parcelPrefabBase)) {
                throw new Exception("Parcel prefab not found");
            }

            var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
            m_TestToolSystem.Enable();
            await WaitFrames(1);
            m_TestToolSystem.TrySetPrefab(parcelPrefabBase);
            m_TestToolSystem.Place(prefabEntity, transform);

            return;
        }

        private async Task PlaceParcel(PrefabID prefabID, Transform transform, ZoneType zoneType) {
            if (!m_PrefabSystem.TryGetPrefab(prefabID, out var parcelPrefabBase)) {
                throw new Exception("Parcel prefab not found");
            }

            var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
            m_TestToolSystem.Enable();
            await WaitFrames(1);
            m_TestToolSystem.TrySetPrefab(parcelPrefabBase);
            m_TestToolSystem.Place(prefabEntity, transform);

            return;
        }
        
        private async Task Execute(SaveGameMetadata saveGameMetadata = null) {
            log.Info("Execute");

            //var map = PrepareMap();

            //if (map == null) {
            //    log.ErrorFormat("Asset {0} was not found. Test {1} skipped.", MapID, this);
            //    return;
            //}

            //// Create the game
            //if (saveGameMetadata != null) {
            //    await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGameMetadata);
            //} else {
            //    await GameManager.instance.Load(GameMode.Game, Purpose.NewGame, map);
            //}

            await WaitFrames();
            await PlaceParcel(new PrefabID("ParcelPrefab", "Parcel 2x2"), ParcelTransform1);

            TR.Describe("1. New Parcels", () => {
                TR.It("1.1. A new parcel should have correct data after creation", () => {});
                TR.It("1.2. A new parcel should have a ParcelSubBlock buffer of size 1.", () => {});
                TR.It("1.3. A parcel's Block should exist, have the right components and number of Cells in its buffer", () => {});
                TR.It("1.4. A parcel's prezone should be 0 when not selected during placement", () => {});
                TR.It("1.5. A parcel's prezone should be set when selected during placement", () => {});
            });

            // # [B] Verify Parcel behavior
            // # ######################################################
            // # 1. New Parcels
            // #      1.1. A new parcel should have correct data after creation
            // #      1.2. A new parcel should have a ParcelSubBlock buffer of size 1.
            // #      1.3. A parcel's Block should exist, have the right components and number of Cells in its buffer
            // #      1.4. A parcel's prezone should be 0 when not selected during placement
            // #      1.5. A parcel's prezone should be set when selected during placement
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