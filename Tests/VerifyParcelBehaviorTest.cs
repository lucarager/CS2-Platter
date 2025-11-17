// <copyright file="VerifyParcelBehaviorTest.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Colossal.Assertions;
using Game.Buildings;
using Game.Common;
using Game.Tools;
using Game.Zones;
using Platter.Components;
using Unity.Collections;

namespace Platter.Tests {
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Colossal.Entities;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;
    using Game;
    using Game.Assets;
    using Game.City;
    using Game.Input;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.SceneFlow;
    using Game.Simulation;
    using Game.UI.InGame;
    using Systems;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.LowLevel;
    using UnityEngine.InputSystem.Users;
    using Transform = Game.Objects.Transform;
    using static Systems.P_PrefabsCreateSystem;

    [TestDescriptor("Platter: Verify Parcel Behavior", Category.Default)]
    public class VerifyParcelBehaviorTest : TestScenario {
        private const  string           MapID            = "1906eb3c6e796ccf29cbef3e4866961e";
        private const  string           SaveID           = "b620f4c6a3bf2ce1a67accf188444c52";
        private const  string           CityName         = "TestCity-1";
        private const  string           Theme            = "European";
        private static float            Elevation0       = 511.9453f;
        private static Transform        ParcelTransform1 = new(new float3(0, Elevation0, 0), new quaternion(0, 0, 0, 1f));
        private static Transform        ParcelTransform2 = new(new float3(60, Elevation0, 0), new quaternion(0, 0, 0, 1f));
        private        PrefabSystem     m_PrefabSystem;
        private        P_TestToolSystem m_TestToolSystem;
        private        P_UISystem       m_UISystem;
        private        TestRunner       TR;

        /// <inheritdoc/>
        protected override async Task OnPrepare() {
            TR = new TestRunner(log);
            log.Info("OnPrepare");

            TestUtils.SetDefaultTestConditions();
            await Task.CompletedTask;

            // Get Systems
            m_TestToolSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_TestToolSystem>();
            m_UISystem       = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<P_UISystem>();
            m_PrefabSystem   = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override async Task OnCleanup() {
            log.Info("OnCleanup");
            await Task.CompletedTask;
        }

        [Test]
        private async Task StartNewGameAndTest() {
            if (!TestUtils.GetSave(SaveID, out var saveGameMetadata)) {
                log.ErrorFormat("Asset {0} was not found. Test {1} skipped.", SaveID, this);
                return;
            }

            await GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, saveGameMetadata);
            await WaitFrames();

            try {
                var parcelEntity1 = await PlaceParcel(new PrefabID("ParcelPrefab", "Parcel 2x2"), ParcelTransform1);
                var parcelEntity2 = await PlaceParcel(new PrefabID("ParcelPrefab", "Parcel 4x6"), ParcelTransform2, 30);

                await TR.Describe(
                    "New Parcels", async () => {
                        var parcel1 = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntity1);
                        var subblockBuffer1 = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntity1);
                        var blockEntity1 = subblockBuffer1[0].m_SubBlock;

                        var parcel2 = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntity2);
                        var subblockBuffer2 = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntity2);

                        //TR.It(
                        //    "A new parcel should have correct data after creation",
                        //    () => {
                        //        Assert.IsNotNull(parcel1);
                        //    });

                        TR.It(
                            "A new parcel should have a ParcelSubBlock buffer of size 1.", () => {
                                Assert.IsTrue(World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<ParcelSubBlock>(parcelEntity1));
                                Assert.AreEqual(subblockBuffer1.Length, 1);
                                Assert.AreEqual(subblockBuffer2.Length, 1);
                            });

                        TR.It(
                            "A parcel's Block should exist with the right components", () => {
                                Assert.IsTrue(World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<Block>(blockEntity1));
                                Assert.IsTrue(World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<ParcelOwner>(blockEntity1));
                                Assert.IsTrue(World.DefaultGameObjectInjectionWorld.EntityManager.HasBuffer<Cell>(blockEntity1));
                            });

                        await TR.It(
                            "A parcel's Block should have the right number of Cells in its buffer", async () => {
                                for (var i = BlockSizes.x; i <= BlockSizes.z; i++)
                                    for (var j = BlockSizes.y; j <= BlockSizes.w; j++) {
                                        var parcelEntity = await PlaceParcel(
                                            new PrefabID("ParcelPrefab", $"Parcel {i}x{j}"),
                                            new Transform(new float3(100, Elevation0, 100), new quaternion(0, 0, 0, 1f)));
                                        await WaitFrames(4);
                                        var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntity);
                                        var blockEntity = subblockBuffer[0].m_SubBlock;
                                        var cellBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<Cell>(blockEntity);
                                        Assert.AreEqual(cellBuffer.Length, i * 6);
                                        var occupiedCells = cellBuffer.Count(cell => (cell.m_State & CellFlags.Blocked) != CellFlags.None);
                                        // Count occupied cells
                                        Assert.AreEqual(occupiedCells, i * j);
                                        await DeleteEntity(parcelEntity);
                                    }
                            });

                        TR.It(
                            "A parcel's prezone should be None when not selected during placement",
                            () => { Assert.AreEqual(parcel1.m_PreZoneType.m_Index, 0); });

                        TR.It(
                            "A parcel's prezone should be set when selected during placement",
                            () => { Assert.AreEqual(parcel2.m_PreZoneType.m_Index, 30); });
                    });

                await DeleteEntity(parcelEntity1);
                await DeleteEntity(parcelEntity2);
            } catch (Exception e) {
                log.ErrorFormat("Error creating parcel.");
            }

            await WaitFrames();

            try {
                await TR.Describe(
                    "Road Connections", async () => {
                        var positionAwayFromRoad = new Transform(new float3(0, Elevation0, 0), new quaternion(0, 0, 0, 1f));
                        var roadPosition1 = new Transform(new float3(40, Elevation0, 20), new quaternion(0, 0, 0, 1f));
                        var roadPosition2 = new Transform(new float3(80, Elevation0, 20), new quaternion(0, 0, 0, 1f));
                        var positionNextToRoad = new Transform(new float3(60, Elevation0, 0), new quaternion(0, 0, 0, 1f));
                        var roadPosition3 = new Transform(new float3(-10, Elevation0, 10), new quaternion(0, 0, 0, 1f));
                        var roadPosition4 = new Transform(new float3(10, Elevation0, 10), new quaternion(0, 0, 0, 1f));

                        var parcelEntityAwayFromRoad = await PlaceParcel(
                            new PrefabID("ParcelPrefab", "Parcel 2x2"), positionAwayFromRoad);
                        var roadEdgEntity = await PlaceEdge(
                            new PrefabID("RoadPrefab", "Small Road"), roadPosition1, roadPosition2);
                        var parcelEntityNextToExistingRoad = await PlaceParcel(
                            new PrefabID("ParcelPrefab", "Parcel 2x2"), positionNextToRoad);

                        await WaitFrames(30);

                        TR.It(
                            "Placing a parcel away from roads should show a road connection notification", () => {
                                var notifications = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<IconElement>(parcelEntityAwayFromRoad);
                                Assert.AreEqual(notifications.Length, 1);
                            });
                        TR.It(
                            "Placing a parcel away from roads should have a null road entity on the parcel", () => {
                                var parcel = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntityAwayFromRoad);
                                Assert.AreEqual(parcel.m_RoadEdge, Entity.Null);
                            });
                        TR.It(
                            "Placing a parcel away from roads should have a null road entity on the parcel's block", () => {
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityAwayFromRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                Assert.IsFalse(World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<Owner>(blockEntity));
                            });
                        TR.It(
                            "A road should have the right components",
                            () => {
                                Assert.IsTrue(World.DefaultGameObjectInjectionWorld.EntityManager.TryGetBuffer<ConnectedParcel>(roadEdgEntity, true, out var _));
                            });
                        TR.It(
                            "Placing a parcel next to a road should connect the parcel to the road", () => {
                                var parcel = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntityNextToExistingRoad);
                                Assert.AreNotEqual(parcel.m_RoadEdge, Entity.Null, ".m_RoadEdge, Entity.Null");
                                Assert.AreEqual(parcel.m_RoadEdge, roadEdgEntity, "parcel.m_RoadEdge, roadEdgEntity");
                            });
                        TR.It(
                            "Placing a parcel next to a road should connect the road to the parcel", () => {
                                var connectedParcelBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ConnectedParcel>(roadEdgEntity);
                                Assert.AreEqual(connectedParcelBuffer.Length, 1);
                                Assert.AreEqual(connectedParcelBuffer[0].m_Parcel, parcelEntityNextToExistingRoad);
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityNextToExistingRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                var blockOwner = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Owner>(blockEntity);
                                Assert.AreEqual(blockOwner.m_Owner, roadEdgEntity);
                            });
                        TR.It(
                            "Placing a parcel next to a road should connect the block to the road", () => {
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityNextToExistingRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                var blockOwner = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Owner>(blockEntity);
                                Assert.AreEqual(blockOwner.m_Owner, roadEdgEntity);
                            });

                        var roadEdgEntity2 = await PlaceEdge(
                            new PrefabID("RoadPrefab", "Small Road"), roadPosition3, roadPosition4);

                        TR.It(
                            "Placing a road next to an existing parcel should connect the parcel to the road", () => {
                                var parcel = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntityNextToExistingRoad);
                                Assert.AreNotEqual(parcel.m_RoadEdge, Entity.Null);
                                Assert.AreEqual(parcel.m_RoadEdge, roadEdgEntity);
                            });
                        TR.It(
                            "Placing a road next to an existing parcel should connect the road to the parcel", () => {
                                var connectedParcelBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ConnectedParcel>(roadEdgEntity);
                                Assert.AreEqual(connectedParcelBuffer.Length, 1);
                                Assert.AreEqual(connectedParcelBuffer[0].m_Parcel, parcelEntityNextToExistingRoad);
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityNextToExistingRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                var blockOwner = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Owner>(blockEntity);
                                Assert.AreEqual(blockOwner.m_Owner, roadEdgEntity);
                            });
                        TR.It(
                            "Placing a road next to an existing parcel should connect the block to the road", () => {
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityNextToExistingRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                var blockOwner = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Owner>(blockEntity);
                                Assert.AreEqual(blockOwner.m_Owner, roadEdgEntity);
                            });

                        await DeleteEntity(roadEdgEntity2);
                        await WaitFrames();

                        TR.It(
                            "Deleting a road next to an existing parcel should disconnect the two",
                            () => {
                                var parcel = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Parcel>(parcelEntityAwayFromRoad);
                                Assert.AreEqual(parcel.m_RoadEdge, Entity.Null);
                                var subblockBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ParcelSubBlock>(parcelEntityAwayFromRoad);
                                var blockEntity = subblockBuffer[0].m_SubBlock;
                                var blockOwner = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentData<Owner>(blockEntity);
                                Assert.AreEqual(blockOwner.m_Owner, Entity.Null);
                            });

                        TR.It(
                            "Deleting a road next to an existing parcel should show a missing road status",
                            () => {
                                var notifications = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<IconElement>(parcelEntityAwayFromRoad);
                                Assert.AreEqual(notifications.Length, 1);
                            });

                        await DeleteEntity(parcelEntityNextToExistingRoad);
                        await WaitFrames();

                        TR.It(
                            "Deleting a parcel connected to a road should remove it from its ConnectedParcel buffer",
                            () => {
                                var connectedParcelBuffer = World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<ConnectedParcel>(roadEdgEntity);
                                Assert.AreEqual(connectedParcelBuffer.Length, 0);
                            });

                        // Cleanup 
                        await DeleteEntity(roadEdgEntity);
                        await DeleteEntity(parcelEntityAwayFromRoad);
                    });
            } catch (Exception e) {
                log.ErrorFormat("Error");
            }

            //await GameManager.instance.MainMenu();
        }

        private async Task<Entity> PlaceParcel(PrefabID prefabID, Transform transform) {
            return await PlaceParcel(prefabID, transform, 0);
        }

        private async Task<Entity> PlaceParcel(PrefabID prefabID, Transform transform, ushort zoneIndex) {
            if (!m_PrefabSystem.TryGetPrefab(prefabID, out var parcelPrefabBase)) {
                throw new Exception("Parcel prefab not found");
            }

            var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
            m_TestToolSystem.Enable();

            await WaitFrames(1);

            m_TestToolSystem.TrySetPrefab(parcelPrefabBase);
            m_UISystem.PreZoneType = new ZoneType() {
                m_Index = zoneIndex,
            };
            var entity = await m_TestToolSystem.PlopObject(prefabEntity, transform);
            await WaitFrames(20);
            return entity;
        }

        private async Task<Entity> PlaceEdge(PrefabID prefabID, Transform transform1, Transform transform2) {
            if (!m_PrefabSystem.TryGetPrefab(prefabID, out var parcelPrefabBase)) {
                throw new Exception("Parcel prefab not found");
            }

            var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
            m_TestToolSystem.Enable();

            await WaitFrames(1);

            m_TestToolSystem.TrySetPrefab(parcelPrefabBase);
            var entity = await m_TestToolSystem.PlopEdge(prefabEntity, transform1, transform2);
            await WaitFrames(20);
            return entity;
        }

        private async Task DeleteEntity(Entity entity) {
            World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<Deleted>(entity);
            await WaitFrames(20);
        }
    }
}