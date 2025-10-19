// <copyright file="P_BuildingPrefabClassifySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Prefabs;
using Unity.Mathematics;

namespace Platter.Systems {
    using Colossal.Entities;
    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Tools;
    using Unity.Burst.CompilerServices;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;
    using Utils;
    using static Game.UI.NameSystem;
    using static Utils.ParcelUtils;

    /// <summary>
    /// System responsible for adding the GrowableBuilding and LinkedParcel components to buildings.
    /// </summary>
    public partial class P_BuildingPrefabClassifySystem : GameSystemBase {
        // Queries
        private EntityQuery m_BuildingPrefabQuery;

        // Logger
        private PrefixedLogger m_Log;

        // Systems
        private PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingPrefabClassifySystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_BuildingPrefabQuery = SystemAPI.QueryBuilder()
                                             .WithAll<PrefabData>()
                                             .WithAny<BuildingData, SpawnableBuildingData>()
                                             .WithNone<BoundarySubObjectData, BoundarySubAreaData, BoundarySubLaneData>()
                                             .Build();

            // Systems
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Update Cycle
            RequireForUpdate(m_BuildingPrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities = m_BuildingPrefabQuery.ToEntityArray(Allocator.Temp);

            foreach (var prefabEntity in entities) {
                var prefab   = m_PrefabSystem.GetPrefab<BuildingPrefab>(prefabEntity);
                var geometry = EntityManager.GetComponentData<ObjectGeometryData>(prefabEntity);

                if (prefab.GetPrefabID().GetName() != "UbPr_FireHouse01") {
                    continue;
                }

                var cornerRightFront = new float3(geometry.m_Bounds.max.x, 0, geometry.m_Bounds.min.z);
                var cornerLeftFront  = new float3(geometry.m_Bounds.min.x, 0, geometry.m_Bounds.min.z);
                var cornerLeftBack = new float3(geometry.m_Bounds.min.x, 0, geometry.m_Bounds.max.z);
                var cornerRightBack = new float3(geometry.m_Bounds.max.x, 0, geometry.m_Bounds.max.z);

                const float shiftBase    = 2f;
                var         shiftLeft    = new float3(-shiftBase, 0f, 0f);
                var         shiftRight   = new float3(shiftBase, 0f, 0f);
                var         shiftForward = new float3(0f, 0f, -shiftBase);
                var         shiftBack    = new float3(0f, 0f, shiftBase);
                var         shiftDown    = new float3(0f, -shiftBase, 0f);
                var         shiftUp      = new float3(0f, shiftBase, 0f);

                var shiftForLowerBound = shiftForward + shiftLeft  + shiftDown;
                var shiftForUpperBound = shiftBack    + shiftRight + shiftUp;

                // Front
                var bounds = new Dictionary<string, Bounds3> {
                    { "front", new Bounds3(
                        cornerLeftFront  + shiftForLowerBound,
                        cornerRightFront + shiftForUpperBound
                    ) },
                    { "left",  new Bounds3(
                        cornerLeftFront  + shiftForLowerBound,
                        cornerLeftBack   + shiftForUpperBound
                    ) },
                    { "rear",  new Bounds3(
                        cornerLeftBack   + shiftForLowerBound,
                        cornerRightBack  + shiftForUpperBound
                    ) },
                    { "right", new Bounds3(
                        cornerRightFront + shiftForLowerBound,
                        cornerRightBack  + shiftForUpperBound
                    ) },
                };

                if (EntityManager.TryGetBuffer<SubAreaNode>(prefabEntity, false, out var nodeBuffer)) {
                    for (var i = 0; i < nodeBuffer.Length; i++) {
                        var node  = nodeBuffer[i];
                        var shift = new float3();
                        m_Log.Debug($"Checking node {i} - {node.m_Position}");

                        foreach (var (name, boundary) in bounds) {
                            if (!MathUtils.Intersect(boundary, node.m_Position)) {
                                continue;
                            }

                            switch (name) {
                                case "front":
                                    shift.z -= 10;
                                    break;
                                case "rear":
                                    shift.z += 10;
                                    break;
                                case "left":
                                    shift.x -= 10;
                                    break;
                                case "right":
                                    shift.x += 10;
                                    break;
                            }
                        }

                        if (math.any(shift)) {
                            node.m_Position += shift;
                            nodeBuffer[i]   = node;
                        }

                    }
                }

                if (EntityManager.TryGetBuffer<Game.Prefabs.SubObject>(prefabEntity, false, out var subObjectBuffer)) {
                    for (var i = 0; i < subObjectBuffer.Length; i++) {
                        var subObject = subObjectBuffer[i];
                        var shift = new float3();

                        foreach (var (name, boundary) in bounds) {
                            if (!MathUtils.Intersect(boundary, subObject.m_Position)) {
                                continue;
                            }

                            switch (name) {
                                case "front":
                                    shift.z -= 10;
                                    break;
                                case "rear":
                                    shift.z += 10;
                                    break;
                                case "left":
                                    shift.x -= 10;
                                    break;
                                case "right":
                                    shift.x += 10;
                                    break;
                            }
                        }

                        if (math.any(shift)) {
                            subObject.m_Position += shift;
                            subObjectBuffer[i] = subObject;
                        }

                    }
                }

                if (EntityManager.TryGetBuffer<Game.Prefabs.SubLane>(prefabEntity, false, out var subLaneBuffer)) {
                    for (var i = 0; i < subLaneBuffer.Length; i++) {
                        var subLane = subLaneBuffer[i];
                        
                        // Create an array for the curve’s control points
                        var points = new[] { subLane.m_Curve.a, subLane.m_Curve.b, subLane.m_Curve.c, subLane.m_Curve.d };
                        var shifts = new float3[4];

                        foreach (var (name, boundary) in bounds) {
                            for (var j = 0; j < points.Length; j++) {
                                if (!MathUtils.Intersect(boundary, points[j]))
                                    continue;

                                ref var shift = ref shifts[j];
                                switch (name) {
                                    case "front":
                                        shift.z -= 10;
                                        break;
                                    case "rear":
                                        shift.z += 10;
                                        break;
                                    case "left":
                                        shift.x -= 10;
                                        break;
                                    case "right":
                                        shift.x += 10;
                                        break;
                                }
                            }
                        }

                        // Apply all shifts back to the curve
                        subLane.m_Curve.a += shifts[0];
                        subLane.m_Curve.b += shifts[1];
                        subLane.m_Curve.c += shifts[2];
                        subLane.m_Curve.d += shifts[3];

                        subLaneBuffer[i] = subLane;
                    }
                }

                if (EntityManager.TryGetBuffer<Game.Prefabs.SubNet>(prefabEntity, false, out var subNetBuffer)) {
                    for (var i = 0; i < subNetBuffer.Length; i++) {
                        var subNet = subNetBuffer[i];

                        // Create an array for the curve’s control points
                        var points = new[] { subNet.m_Curve.a, subNet.m_Curve.b, subNet.m_Curve.c, subNet.m_Curve.d };
                        var shifts = new float3[4];

                        foreach (var (name, boundary) in bounds) {
                            for (var j = 0; j < points.Length; j++) {
                                if (!MathUtils.Intersect(boundary, points[j]))
                                    continue;

                                ref var shift = ref shifts[j];
                                switch (name) {
                                    case "front":
                                        shift.z -= 10;
                                        break;
                                    case "rear":
                                        shift.z += 10;
                                        break;
                                    case "left":
                                        shift.x -= 10;
                                        break;
                                    case "right":
                                        shift.x += 10;
                                        break;
                                }
                            }
                        }

                        // Apply all shifts back to the curve
                        subNet.m_Curve.a += shifts[0];
                        subNet.m_Curve.b += shifts[1];
                        subNet.m_Curve.c += shifts[2];
                        subNet.m_Curve.d += shifts[3];

                        subNetBuffer[i] = subNet;
                    }
                }


                EntityManager.AddBuffer<BoundarySubObjectData>(prefabEntity);
                EntityManager.AddBuffer<BoundarySubAreaData>(prefabEntity);
                EntityManager.AddBuffer<BoundarySubLaneData>(prefabEntity);
            }
        }
    }
}