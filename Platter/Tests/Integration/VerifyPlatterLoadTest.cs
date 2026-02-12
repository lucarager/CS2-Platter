// <copyright file="VerifyPlatterLoadTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

#region Using Statements

using ConnectedBuilding = Game.Buildings.ConnectedBuilding;
using Edge = Game.Net.Edge;

#endregion

namespace Platter.Tests {
    #region Using Statements

    using System;
    using System.Threading.Tasks;
    using Colossal.Assertions;
    using Colossal.IO.AssetDatabase;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;
    using Components;
    using Game;
    using Game.Assets;
    using Game.Buildings;
    using Game.City;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Simulation;
    using Systems;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Utils;
    using Hash128 = Colossal.Hash128;

    #endregion

    [TestDescriptor("Platter: Verify Load Integrity", Category.Default)]
    public class VerifyPlatterLoadTest : TestScenario {
        private const string        CityName = "TestCity-1";
        private const string        MapID    = "1906eb3c6e796ccf29cbef3e4866961e";
        private const string        Save1    = "b620f4c6a3bf2ce1a67accf188444c52";
        private const string        Theme    = "European";
        private       EntityManager m_EM;

        // Queries
        private EntityQuery          m_Query_EdgesWithoutConnectedParcelBuffer;
        private EntityQuery          m_Query_GrowableBuildingWithoutRequired;
        private P_ParcelSearchSystem m_P_ParcelSearchSystem;

        // Systems
        private P_ZoneCacheSystem m_P_ZoneCacheSystem;
        private PrefabSystem      m_PrefabSystem;

        private TestRunner TR;

        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            TR = new TestRunner(log);
            log.Info("OnPrepare");

            TestUtils.SetDefaultTestConditions();
            await Task.CompletedTask;

            m_EM = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Write Queries
            m_Query_EdgesWithoutConnectedParcelBuffer =
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Edge, ConnectedBuilding>()
                    .WithNone<ConnectedParcel>()
                    .Build(m_EM);

            m_Query_GrowableBuildingWithoutRequired =
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Building>()
                    .WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>()
                    .WithNone<LinkedParcel, GrowableBuilding>()
                    .Build(m_EM);

            // Get Systems
            m_P_ZoneCacheSystem    = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<P_ZoneCacheSystem>();
            m_P_ParcelSearchSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_ParcelSearchSystem>();
            m_PrefabSystem         = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [Test]
        private Task StartNewGameAndVerifyIntegrity() { return Execute(); }

        [Test]
        private Task LoadGameAndVerifyIntegrity() {
            if (TestUtils.GetSave(Save1, out var saveGameMetadata)) {
                return Execute(saveGameMetadata);
            }

            return Task.FromException(new Exception("Savegame not found."));
        }

        private MapMetadata PrepareMap() {
            var existingSystemManaged = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CityConfigurationSystem>();
            existingSystemManaged.overrideLoadedOptions                                               = true;
            existingSystemManaged.overrideCityName                                                    = CityName;
            existingSystemManaged.overrideThemeName                                                   = Theme;
            existingSystemManaged.overrideLeftHandTraffic                                             = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<TimeSystem>().startingYear = DateTime.Now.Year;

            return AssetDatabase.global.GetAsset<MapMetadata>(Hash128.Parse(MapID));
        }

        private async Task Execute(SaveGameMetadata saveGameMetadata = null) {
            log.Info("Execute");


            // Create the game
            if (saveGameMetadata != null) {
                await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGameMetadata);
            } else {
                var map = PrepareMap();

                if (map == null) {
                    log.ErrorFormat("Asset {0} was not found. Test {1} skipped.", MapID, this);
                    return;
                }

                await GameManager.instance.Load(GameMode.Game, Purpose.NewGame, map);
            }

            await WaitFrames();

            TR.Describe(
                "1. Roads",
                () => {
                    TR.It(
                        "1.1. All existing roads with zoning must have ConnectedParcel Buffer",
                        () => Assert.IsTrue(m_Query_EdgesWithoutConnectedParcelBuffer.IsEmpty));
                });

            TR.Describe(
                "2. Zones",
                () => {
                    TR.It("2.1. ZoneCacheSystem should have a minimum of 10 cached zones", () => Assert.IsTrue(m_P_ZoneCacheSystem.ZonePrefabs.Count >= 10));
                });

            TR.Describe(
                "3. Growables",
                () => {
                    TR.It(
                        "3.1. All growable buildings must have GrowableBuilding and LinkedParcel components",
                        () => Assert.IsTrue(m_Query_GrowableBuildingWithoutRequired.IsEmpty));
                });

            TR.Describe(
                "4. Prefabs",
                () => {
                    var blockSizes = P_PrefabsCreateSystem.AvailableParcelLotSizes;

                    for (var x = blockSizes.x; x <= blockSizes.z; x++)
                    for (var y = blockSizes.y; y <= blockSizes.w; y++) {
                        var id      = new PrefabID("ParcelPrefab", $"Parcel {x}x{y}");
                        var lotSize = new int2(x, y);

                        var hasPrefab = m_PrefabSystem.TryGetPrefab(id, out var prefabBase);
                        TR.It($"4.1. Parcel {id}'s prefabBase should exist", () => Assert.IsTrue(hasPrefab));

                        var pEntity   = m_PrefabSystem.GetEntity(prefabBase);
                        var parcelGeo = new ParcelGeometry(lotSize);
                        var oGeoData  = m_EM.GetComponentData<ObjectGeometryData>(pEntity);
                        TR.It($"4.2. Parcel {id}'s prefabBase should have correct size", () => Assert.AreEqual(oGeoData.m_Size, parcelGeo.Size));
                        TR.It($"4.2. Parcel {id}'s prefabBase should have correct pivot", () => Assert.AreEqual(oGeoData.m_Pivot, parcelGeo.Pivot));
                        TR.It($"4.2. Parcel {id}'s prefabBase should have correct bounds", () => Assert.AreEqual(oGeoData.m_Bounds, parcelGeo.Bounds));

                        var parcelbData = m_EM.GetComponentData<ParcelData>(pEntity);
                        TR.It(
                            $"4.3. Parcel {id}'s prefabBase should have a block prefab entity cached",
                            () => Assert.AreNotEqual(Entity.Null, parcelbData.m_ZoneBlockPrefab));
                    }
                });

            TR.Describe(
                "5. ParcelSearchSystem",
                () => { TR.It("5.1. ParcelSearchSystem should be initialized and loaded", () => Assert.IsTrue(m_P_ParcelSearchSystem.Loaded)); });

            await GameManager.instance.MainMenu();

            TR.Complete();
        }
    }
}