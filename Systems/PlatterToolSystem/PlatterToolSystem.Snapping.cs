// <copyright file="PlatterToolSystem.Snapping.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        private JobHandle MakeSnapControlJob(JobHandle inputDeps) {
            var snapJobData = new SnapJob() {
                m_EditorMode = m_ToolSystem.actionMode.IsEditor(),
                m_Snap = GetActualSnap(),

                // m_Mode = actualMode,
                m_Prefab = m_PrefabSystem.GetEntity(m_Prefab),
                m_Selected = m_ToolSystem.selected,

                // m_OwnerData = __TypeHandle.__Game_Common_Owner_RO_ComponentLookup,
                // m_TransformData = __TypeHandle.__Game_Objects_Transform_RO_ComponentLookup,
                // m_AttachedData = __TypeHandle.__Game_Objects_Attached_RO_ComponentLookup,
                // m_TerrainData = __TypeHandle.__Game_Common_Terrain_RO_ComponentLookup,
                // m_LocalTransformCacheData = __TypeHandle.__Game_Tools_LocalTransformCache_RO_ComponentLookup,
                // m_EdgeData = __TypeHandle.__Game_Net_Edge_RO_ComponentLookup,
                // m_NodeData = __TypeHandle.__Game_Net_Node_RO_ComponentLookup,
                // m_OrphanData = __TypeHandle.__Game_Net_Orphan_RO_ComponentLookup,
                // m_CurveData = __TypeHandle.__Game_Net_Curve_RO_ComponentLookup,
                // m_CompositionData = __TypeHandle.__Game_Net_Composition_RO_ComponentLookup,
                // m_EdgeGeometryData = __TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentLookup,
                // m_StartNodeGeometryData =
                // __TypeHandle.__Game_Net_StartNodeGeometry_RO_ComponentLookup,
                // m_EndNodeGeometryData =
                // __TypeHandle.__Game_Net_EndNodeGeometry_RO_ComponentLookup,
                // m_PrefabRefData = __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup,
                // m_ObjectGeometryData =
                // __TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup,
                // m_BuildingData = __TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup,
                // m_BuildingExtensionData =
                // __TypeHandle.__Game_Prefabs_BuildingExtensionData_RO_ComponentLookup,
                // m_PrefabCompositionData =
                // __TypeHandle.__Game_Prefabs_NetCompositionData_RO_ComponentLookup,
                // m_PlaceableObjectData =
                // __TypeHandle.__Game_Prefabs_PlaceableObjectData_RO_ComponentLookup,
                // m_MovingObjectData =
                // __TypeHandle.__Game_Prefabs_MovingObjectData_RO_ComponentLookup,
                // m_AssetStampData =
                // __TypeHandle.__Game_Prefabs_AssetStampData_RO_ComponentLookup,
                // m_OutsideConnectionData =
                // __TypeHandle.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup,
                // m_NetObjectData = __TypeHandle.__Game_Prefabs_NetObjectData_RO_ComponentLookup,
                // m_TransportStopData =
                // __TypeHandle.__Game_Prefabs_TransportStopData_RO_ComponentLookup,
                // m_StackData = __TypeHandle.__Game_Prefabs_StackData_RO_ComponentLookup,
                // m_ServiceUpgradeData =
                // __TypeHandle.__Game_Prefabs_ServiceUpgradeData_RO_ComponentLookup,
                // m_NetData = __TypeHandle.__Game_Prefabs_NetData_RO_ComponentLookup,
                // m_NetGeometryData =
                // __TypeHandle.__Game_Prefabs_NetGeometryData_RO_ComponentLookup,
                // m_RoadData = __TypeHandle.__Game_Prefabs_RoadData_RO_ComponentLookup,
                // m_BlockData = __TypeHandle.__Game_Zones_Block_RO_ComponentLookup,
                // m_SubObjects = __TypeHandle.__Game_Objects_SubObject_RO_BufferLookup,
                // m_ConnectedEdges = __TypeHandle.__Game_Net_ConnectedEdge_RO_BufferLookup,
                // m_PrefabCompositionAreas =
                // __TypeHandle.__Game_Prefabs_NetCompositionArea_RO_BufferLookup,
                // m_PrefabSubNets = __TypeHandle.__Game_Prefabs_SubNet_RO_BufferLookup,
                m_ObjectSearchTree = m_ObjectSearchSystem.GetStaticSearchTree(true, out var objectSearchDeps),
                m_NetSearchTree = m_NetSearchSystem.GetNetSearchTree(true, out var netSearchDeps),
                m_ZoneSearchTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneSearchDeps),
                m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var deps),

                // m_TerrainHeightData = m_TerrainSystem.GetHeightData(false),
                // m_ControlPoints = m_ControlPoints,
                // m_SubSnapPoints = m_SubSnapPoints,
                // m_Rotation = m_Rotation,
            };

            var jobHandle = snapJobData.Schedule(
                JobUtils.CombineDependencies(inputDeps, objectSearchDeps, netSearchDeps, zoneSearchDeps, deps));
            m_ObjectSearchSystem.AddStaticSearchTreeReader(jobHandle);
            m_NetSearchSystem.AddNetSearchTreeReader(jobHandle);
            m_ZoneSearchSystem.AddSearchTreeReader(jobHandle);
            m_WaterSystem.AddSurfaceReader(jobHandle);
            return jobHandle;
        }

        private struct SnapJob : IJob {
            [ReadOnly] public bool m_EditorMode;
            [ReadOnly] public Snap m_Snap;

            // [ReadOnly] public Mode m_Mode;
            [ReadOnly] public Entity m_Prefab;
            [ReadOnly] public Entity m_Selected;
            [ReadOnly] public ComponentLookup<Owner> m_OwnerData;
            [ReadOnly] public ComponentLookup<Game.Objects.Transform> m_TransformData;
            [ReadOnly] public ComponentLookup<Attached> m_AttachedData;
            [ReadOnly] public ComponentLookup<Terrain> m_TerrainData;
            [ReadOnly] public ComponentLookup<LocalTransformCache> m_LocalTransformCacheData;
            [ReadOnly] public ComponentLookup<Edge> m_EdgeData;
            [ReadOnly] public ComponentLookup<Game.Net.Node> m_NodeData;
            [ReadOnly] public ComponentLookup<Orphan> m_OrphanData;
            [ReadOnly] public ComponentLookup<Curve> m_CurveData;
            [ReadOnly] public ComponentLookup<Composition> m_CompositionData;
            [ReadOnly] public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;
            [ReadOnly] public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;
            [ReadOnly] public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
            [ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
            [ReadOnly] public ComponentLookup<BuildingData> m_BuildingData;
            [ReadOnly] public ComponentLookup<BuildingExtensionData> m_BuildingExtensionData;
            [ReadOnly] public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
            [ReadOnly] public ComponentLookup<PlaceableObjectData> m_PlaceableObjectData;
            [ReadOnly] public ComponentLookup<MovingObjectData> m_MovingObjectData;
            [ReadOnly] public ComponentLookup<AssetStampData> m_AssetStampData;
            [ReadOnly] public ComponentLookup<OutsideConnectionData> m_OutsideConnectionData;
            [ReadOnly] public ComponentLookup<NetObjectData> m_NetObjectData;
            [ReadOnly] public ComponentLookup<TransportStopData> m_TransportStopData;
            [ReadOnly] public ComponentLookup<StackData> m_StackData;
            [ReadOnly] public ComponentLookup<ServiceUpgradeData> m_ServiceUpgradeData;
            [ReadOnly] public ComponentLookup<NetData> m_NetData;
            [ReadOnly] public ComponentLookup<NetGeometryData> m_NetGeometryData;
            [ReadOnly] public ComponentLookup<RoadData> m_RoadData;
            [ReadOnly] public ComponentLookup<Block> m_BlockData;
            [ReadOnly] public BufferLookup<Game.Objects.SubObject> m_SubObjects;
            [ReadOnly] public BufferLookup<ConnectedEdge> m_ConnectedEdges;
            [ReadOnly] public BufferLookup<NetCompositionArea> m_PrefabCompositionAreas;
            [ReadOnly] public BufferLookup<Game.Prefabs.SubNet> m_PrefabSubNets;
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ObjectSearchTree;
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly] public NativeQuadTree<Entity, Bounds2> m_ZoneSearchTree;
            [ReadOnly] public WaterSurfaceData m_WaterSurfaceData;
            [ReadOnly] public TerrainHeightData m_TerrainHeightData;

            public NativeList<ControlPoint> m_ControlPoints;

            public NativeList<SubSnapPoint> m_SubSnapPoints;

            public NativeValue<Rotation> m_Rotation;

            public void Execute() {
                // m_SubSnapPoints.Clear();
                // var controlPoint = m_ControlPoints[0];
                // if ((m_Snap & (Snap.NetArea | Snap.NetNode)) != Snap.None &&
                //    m_TerrainData.HasComponent(controlPoint.m_OriginalEntity) &&
                //    !m_BuildingData.HasComponent(m_Prefab)) {
                //    FindLoweredParent(ref controlPoint);
                // }

                // var bestSnapPosition = controlPoint;
                // bestSnapPosition.m_OriginalEntity = Entity.Null;
                // if (m_OutsideConnectionData.HasComponent(m_Prefab)) {
                //    HandleWorldSize(ref bestSnapPosition, controlPoint);
                // }

                // var waterSurfaceHeight = float.MinValue;
                // if ((m_Snap & Snap.Shoreline) != Snap.None) {
                //    var radius = 1f;
                //    float3 offset = 0f;
                //    if (m_BuildingData.TryGetComponent(m_Prefab, out BuildingData componentData)) {
                //        radius = math.length(componentData.m_LotSize) * 4f;
                //    } else if (m_BuildingExtensionData.TryGetComponent(m_Prefab, out BuildingExtensionData componentData2)) {
                //        radius = math.length(componentData2.m_LotSize) * 4f;
                //    }

                // if (m_PlaceableObjectData.TryGetComponent(m_Prefab, out PlaceableObjectData componentData3)) {
                //        offset = componentData3.m_PlacementOffset;
                //    }

                // SnapShoreline(controlPoint, ref bestSnapPosition, ref waterSurfaceHeight,
                //        radius, offset);
                // }

                // if ((m_Snap & Snap.NetSide) != Snap.None) {
                //    var buildingData = m_BuildingData[m_Prefab];
                //    var num = (buildingData.m_LotSize.y * 4f) + 16f;
                //    var bestDistance = (math.cmin(buildingData.m_LotSize) * 4f) + 16f;
                //    var zoneBlockIterator = default(ZoneBlockIterator);
                //    zoneBlockIterator.m_ControlPoint = controlPoint;
                //    zoneBlockIterator.m_BestSnapPosition = bestSnapPosition;
                //    zoneBlockIterator.m_BestDistance = bestDistance;
                //    zoneBlockIterator.m_LotSize = buildingData.m_LotSize;
                //    zoneBlockIterator.m_Bounds = new Bounds2(
                //        controlPoint.m_Position.xz - num,
                //        controlPoint.m_Position.xz + num);
                //    zoneBlockIterator.m_Direction = math.forward(m_Rotation.value.m_Rotation).xz;
                //    zoneBlockIterator.m_IgnoreOwner =
                //        m_Mode == Mode.Move ? m_Selected : Entity.Null;
                //    zoneBlockIterator.m_OwnerData = m_OwnerData;
                //    zoneBlockIterator.m_BlockData = m_BlockData;
                //    var iterator = zoneBlockIterator;
                //    m_ZoneSearchTree.Iterate<ZoneBlockIterator>(ref iterator, 0);
                //    bestSnapPosition = iterator.m_BestSnapPosition;
                // }

                // if ((m_Snap & Snap.ExistingGeometry) != Snap.None &&
                //    m_PrefabSubNets.TryGetBuffer(m_Prefab, out DynamicBuffer<Prefabs.SubNet> bufferData)) {
                //    var num2 = 2f;
                //    if (m_Mode == Mode.Stamp) {
                //        for (var i = 0; i < bufferData.Length; i++) {
                //            var subNet = bufferData[i];
                //            if (subNet.m_Snapping.x) {
                //                num2 = math.clamp(math.length(subNet.m_Curve.a.xz) * 0.02f, num2,
                //                    4f);
                //            }

                // if (subNet.m_Snapping.y) {
                //                num2 = math.clamp(math.length(subNet.m_Curve.d.xz) * 0.02f, num2,
                //                    4f);
                //            }
                //        }
                //    }

                // var netIterator = default(NetIterator);
                //    netIterator.m_ControlPoint = controlPoint;
                //    netIterator.m_BestSnapPosition = bestSnapPosition;
                //    netIterator.m_Rotation = m_Rotation.value.m_Rotation;
                //    netIterator.m_IgnoreOwner = m_Mode == Mode.Move ? m_Selected : Entity.Null;
                //    netIterator.m_SnapFactor = 1f / num2;
                //    netIterator.m_SubSnapPoints = m_SubSnapPoints;
                //    netIterator.m_OwnerData = m_OwnerData;
                //    netIterator.m_NodeData = m_NodeData;
                //    netIterator.m_EdgeData = m_EdgeData;
                //    netIterator.m_CurveData = m_CurveData;
                //    netIterator.m_PrefabRefData = m_PrefabRefData;
                //    netIterator.m_PrefabNetData = m_NetData;
                //    netIterator.m_PrefabNetGeometryData = m_NetGeometryData;
                //    netIterator.m_ConnectedEdges = m_ConnectedEdges;
                //    var iterator2 = netIterator;
                //    for (var j = 0; j < bufferData.Length; j++) {
                //        var subNet2 = bufferData[j];
                //        if (subNet2.m_Snapping.x) {
                //            var xz = ObjectUtils.LocalToWorld(
                //                controlPoint.m_HitPosition,
                //                controlPoint.m_Rotation, subNet2.m_Curve.a).xz;
                //            iterator2.m_Bounds = new Bounds2(xz - (8f * num2), xz + (8f * num2));
                //            iterator2.m_LocalOffset = subNet2.m_Curve.a;
                //            iterator2.m_LocalTangent = math.select(
                //                default,
                //                math.normalizesafe(
                //                    MathUtils.StartTangent(subNet2.m_Curve).xz,
                //                    default), subNet2.m_NodeIndex.y != subNet2.m_NodeIndex.x);
                //            m_NetData.TryGetComponent(subNet2.m_Prefab, out iterator2.m_NetData);
                //            m_NetGeometryData.TryGetComponent(
                //                subNet2.m_Prefab,
                //                out iterator2.m_NetGeometryData);
                //            m_RoadData.TryGetComponent(subNet2.m_Prefab, out iterator2.m_RoadData);
                //            m_NetSearchTree.Iterate<NetIterator>(ref iterator2, 0);
                //        }

                // if (subNet2.m_Snapping.y) {
                //            var xz2 = ObjectUtils.LocalToWorld(
                //                controlPoint.m_HitPosition,
                //                controlPoint.m_Rotation, subNet2.m_Curve.d).xz;
                //            iterator2.m_Bounds = new Bounds2(xz2 - (8f * num2), xz2 + (8f * num2));
                //            iterator2.m_LocalOffset = subNet2.m_Curve.d;
                //            iterator2.m_LocalTangent =
                //                math.normalizesafe(
                //                    -MathUtils.EndTangent(subNet2.m_Curve).xz,
                //                    default);
                //            m_NetData.TryGetComponent(subNet2.m_Prefab, out iterator2.m_NetData);
                //            m_NetGeometryData.TryGetComponent(
                //                subNet2.m_Prefab,
                //                out iterator2.m_NetGeometryData);
                //            m_RoadData.TryGetComponent(subNet2.m_Prefab, out iterator2.m_RoadData);
                //            m_NetSearchTree.Iterate<NetIterator>(ref iterator2, 0);
                //        }
                //    }

                // bestSnapPosition = iterator2.m_BestSnapPosition;
                // }

                // if ((m_Snap & Snap.OwnerSide) != Snap.None) {
                //    var entity = Entity.Null;
                //    if (m_Mode == Mode.Upgrade) {
                //        entity = m_Selected;
                //    } else if (m_Mode == Mode.Move &&
                //             m_OwnerData.TryGetComponent(m_Selected, out Owner componentData4)) {
                //        entity = componentData4.m_Owner;
                //    }

                // if (entity != Entity.Null) {
                //        var buildingData2 = m_BuildingData[m_Prefab];
                //        var prefabRef = m_PrefabRefData[entity];
                //        var transform = m_TransformData[entity];
                //        var buildingData3 = m_BuildingData[prefabRef.m_Prefab];
                //        var lotSize = buildingData3.m_LotSize + buildingData2.m_LotSize.y;
                //        var xz3 = BuildingUtils.CalculateCorners(transform, lotSize).xz;
                //        var num3 = buildingData2.m_LotSize.x - 1;
                //        var flag = false;
                //        if (m_ServiceUpgradeData.TryGetComponent(m_Prefab, out ServiceUpgradeData componentData5)) {
                //            num3 = math.select(num3, componentData5.m_MaxPlacementOffset,
                //                componentData5.m_MaxPlacementOffset >= 0);
                //            flag |= componentData5.m_MaxPlacementDistance == 0f;
                //        }

                // if (!flag) {
                //            float2 halfLotSize = (buildingData2.m_LotSize * 4f) - 0.4f;
                //            var xz5 = BuildingUtils
                //                .CalculateCorners(transform, buildingData3.m_LotSize).xz;
                //            var xz4 = BuildingUtils.CalculateCorners(
                //                controlPoint.m_HitPosition,
                //                m_Rotation.value.m_Rotation, halfLotSize).xz;
                //            flag = MathUtils.Intersect(xz5, xz4) &&
                //                   MathUtils.Intersect(xz3, controlPoint.m_HitPosition.xz);
                //        }

                // CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition,
                //            new Line2(xz3.a, xz3.b), num3, 0f, flag);
                //        CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition,
                //            new Line2(xz3.b, xz3.c), num3, 1.5707964f, flag);
                //        CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition,
                //            new Line2(xz3.c, xz3.d), num3, 3.1415927f, flag);
                //        CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition,
                //            new Line2(xz3.d, xz3.a), num3, 4.712389f, flag);
                //    }
                // }

                // if ((m_Snap & Snap.NetArea) != Snap.None) {
                //    if (m_BuildingData.HasComponent(m_Prefab)) {
                //        if (m_CurveData.TryGetComponent(
                //            controlPoint.m_OriginalEntity,
                //            out Curve componentData6)) {
                //            var snapPosition = controlPoint;
                //            snapPosition.m_OriginalEntity = Entity.Null;
                //            snapPosition.m_Position = MathUtils.Position(
                //                componentData6.m_Bezier,
                //                controlPoint.m_CurvePosition);
                //            snapPosition.m_Direction = math.normalizesafe(
                //                MathUtils.Tangent(
                //                    componentData6.m_Bezier,
                //                    controlPoint.m_CurvePosition).xz, default);
                //            snapPosition.m_Direction = MathUtils.Left(snapPosition.m_Direction);
                //            if (math.dot(
                //                math.forward(m_Rotation.value.m_Rotation).xz,
                //                snapPosition.m_Direction) <
                //                0f) {
                //                snapPosition.m_Direction = -snapPosition.m_Direction;
                //            }

                // snapPosition.m_Rotation =
                //                ToolUtils.CalculateRotation(snapPosition.m_Direction);
                //            snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f,
                //                controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz,
                //                snapPosition.m_Direction);
                //            AddSnapPosition(ref bestSnapPosition, snapPosition);
                //        }
                //    } else if (m_EdgeGeometryData.HasComponent(controlPoint.m_OriginalEntity)) {
                //        var edgeGeometry = m_EdgeGeometryData[controlPoint.m_OriginalEntity];
                //        var composition = m_CompositionData[controlPoint.m_OriginalEntity];
                //        var prefabCompositionData = m_PrefabCompositionData[composition.m_Edge];
                //        var areas = m_PrefabCompositionAreas[composition.m_Edge];
                //        var num4 = 0f;
                //        if (m_ObjectGeometryData.HasComponent(m_Prefab)) {
                //            var objectGeometryData = m_ObjectGeometryData[m_Prefab];
                //            if ((objectGeometryData.m_Flags & Objects.GeometryFlags.Standing) !=
                //                Objects.GeometryFlags.None) {
                //                num4 = objectGeometryData.m_LegSize.z * 0.5f;
                //                if (objectGeometryData.m_LegSize.y <=
                //                    prefabCompositionData.m_HeightRange.max) {
                //                    num4 = math.max(num4, objectGeometryData.m_Size.z * 0.5f);
                //                }
                //            } else {
                //                num4 = objectGeometryData.m_Size.z * 0.5f;
                //            }
                //        }

                // SnapSegmentAreas(controlPoint, ref bestSnapPosition, num4,
                //            controlPoint.m_OriginalEntity, edgeGeometry.m_Start,
                //            prefabCompositionData, areas);
                //        SnapSegmentAreas(controlPoint, ref bestSnapPosition, num4,
                //            controlPoint.m_OriginalEntity, edgeGeometry.m_End,
                //            prefabCompositionData, areas);
                //    } else if (m_ConnectedEdges.HasBuffer(controlPoint.m_OriginalEntity)) {
                //        var dynamicBuffer = m_ConnectedEdges[controlPoint.m_OriginalEntity];
                //        for (var k = 0; k < dynamicBuffer.Length; k++) {
                //            var edge = dynamicBuffer[k].m_Edge;
                //            var edge2 = m_EdgeData[edge];
                //            if ((!(edge2.m_Start != controlPoint.m_OriginalEntity) ||
                //                 !(edge2.m_End != controlPoint.m_OriginalEntity)) &&
                //                m_EdgeGeometryData.HasComponent(edge)) {
                //                var edgeGeometry2 = m_EdgeGeometryData[edge];
                //                var composition2 = m_CompositionData[edge];
                //                var prefabCompositionData2 =
                //                    m_PrefabCompositionData[composition2.m_Edge];
                //                var areas2 = m_PrefabCompositionAreas[composition2.m_Edge];
                //                var num5 = 0f;
                //                if (m_ObjectGeometryData.HasComponent(m_Prefab)) {
                //                    var objectGeometryData2 = m_ObjectGeometryData[m_Prefab];
                //                    if ((objectGeometryData2.m_Flags &
                //                         Objects.GeometryFlags.Standing) !=
                //                        Objects.GeometryFlags.None) {
                //                        num5 = objectGeometryData2.m_LegSize.z * 0.5f;
                //                        if (objectGeometryData2.m_LegSize.y <=
                //                            prefabCompositionData2.m_HeightRange.max) {
                //                            num5 = math.max(
                //                                num5,
                //                                objectGeometryData2.m_Size.z * 0.5f);
                //                        }
                //                    } else {
                //                        num5 = objectGeometryData2.m_Size.z * 0.5f;
                //                    }
                //                }

                // SnapSegmentAreas(controlPoint, ref bestSnapPosition, num5, edge,
                //                    edgeGeometry2.m_Start, prefabCompositionData2, areas2);
                //                SnapSegmentAreas(controlPoint, ref bestSnapPosition, num5, edge,
                //                    edgeGeometry2.m_End, prefabCompositionData2, areas2);
                //            }
                //        }
                //    }
                // }

                // if ((m_Snap & Snap.NetNode) != Snap.None) {
                //    if (m_NodeData.HasComponent(controlPoint.m_OriginalEntity)) {
                //        var node = m_NodeData[controlPoint.m_OriginalEntity];
                //        SnapNode(controlPoint, ref bestSnapPosition, controlPoint.m_OriginalEntity,
                //            node);
                //    } else if (m_EdgeData.HasComponent(controlPoint.m_OriginalEntity)) {
                //        var edge3 = m_EdgeData[controlPoint.m_OriginalEntity];
                //        SnapNode(controlPoint, ref bestSnapPosition, edge3.m_Start,
                //            m_NodeData[edge3.m_Start]);
                //        SnapNode(controlPoint, ref bestSnapPosition, edge3.m_End,
                //            m_NodeData[edge3.m_End]);
                //    }
                // }

                // if ((m_Snap & Snap.ObjectSurface) != Snap.None &&
                //    m_TransformData.HasComponent(controlPoint.m_OriginalEntity)) {
                //    var parentMesh = controlPoint.m_ElementIndex.x;
                //    var entity2 = controlPoint.m_OriginalEntity;
                //    while (m_OwnerData.HasComponent(entity2)) {
                //        if (m_LocalTransformCacheData.HasComponent(entity2)) {
                //            parentMesh = m_LocalTransformCacheData[entity2].m_ParentMesh;
                //            parentMesh += math.select(1000, -1000, parentMesh < 0);
                //        }

                // entity2 = m_OwnerData[entity2].m_Owner;
                //    }

                // if (m_TransformData.HasComponent(entity2) && m_SubObjects.HasBuffer(entity2)) {
                //        SnapSurface(controlPoint, ref bestSnapPosition, entity2, parentMesh);
                //    }
                // }

                // CalculateHeight(ref bestSnapPosition, waterSurfaceHeight);
                // if (m_EditorMode) {
                //    if ((m_Snap & Snap.AutoParent) == Snap.None) {
                //        if ((m_Snap & (Snap.NetArea | Snap.NetNode)) == Snap.None ||
                //            m_TransformData.HasComponent(bestSnapPosition.m_OriginalEntity) ||
                //            m_BuildingData.HasComponent(m_Prefab)) {
                //            bestSnapPosition.m_OriginalEntity = Entity.Null;
                //        }
                //    } else if (bestSnapPosition.m_OriginalEntity == Entity.Null) {
                //        var objectGeometryData3 = default(ObjectGeometryData);
                //        if (m_ObjectGeometryData.HasComponent(m_Prefab)) {
                //            objectGeometryData3 = m_ObjectGeometryData[m_Prefab];
                //        }

                // var parentObjectIterator = default(ParentObjectIterator);
                //        parentObjectIterator.m_ControlPoint = bestSnapPosition;
                //        parentObjectIterator.m_BestSnapPosition = bestSnapPosition;
                //        parentObjectIterator.m_Bounds = ObjectUtils.CalculateBounds(
                //            bestSnapPosition.m_Position, bestSnapPosition.m_Rotation,
                //            objectGeometryData3);
                //        parentObjectIterator.m_BestOverlap = float.MaxValue;
                //        parentObjectIterator.m_IsBuilding = m_BuildingData.HasComponent(m_Prefab);
                //        parentObjectIterator.m_PrefabObjectGeometryData1 = objectGeometryData3;
                //        parentObjectIterator.m_TransformData = m_TransformData;
                //        parentObjectIterator.m_BuildingData = m_BuildingData;
                //        parentObjectIterator.m_AssetStampData = m_AssetStampData;
                //        parentObjectIterator.m_PrefabRefData = m_PrefabRefData;
                //        parentObjectIterator.m_PrefabObjectGeometryData = m_ObjectGeometryData;
                //        var iterator3 = parentObjectIterator;
                //        m_ObjectSearchTree.Iterate<ParentObjectIterator>(ref iterator3, 0);
                //        bestSnapPosition = iterator3.m_BestSnapPosition;
                //    }
                // }

                // if (m_Mode == Mode.Create && m_NetObjectData.HasComponent(m_Prefab) &&
                //    (m_NodeData.HasComponent(bestSnapPosition.m_OriginalEntity) ||
                //     m_EdgeData.HasComponent(bestSnapPosition.m_OriginalEntity))) {
                //    FindOriginalObject(ref bestSnapPosition, controlPoint);
                // }

                // var value = m_Rotation.value;
                // value.m_IsAligned &= value.m_Rotation.Equals(bestSnapPosition.m_Rotation);
                // AlignObject(ref bestSnapPosition, ref value.m_ParentRotation, value.m_IsAligned);
                // value.m_Rotation = bestSnapPosition.m_Rotation;
                // m_Rotation.value = value;
                // if ((bestSnapPosition.m_OriginalEntity == Entity.Null ||
                //     bestSnapPosition.m_ElementIndex.x == -1 ||
                //     bestSnapPosition.m_HitDirection.y > 0.99f) &&
                //    m_ObjectGeometryData.TryGetComponent(m_Prefab, out ObjectGeometryData componentData7) &&
                //    componentData7.m_Bounds.min.y <= -0.01f &&
                //    ((m_PlaceableObjectData.TryGetComponent(m_Prefab, out PlaceableObjectData componentData8) &&
                //      (componentData8.m_Flags &
                //       (Objects.PlacementFlags.Wall | Objects.PlacementFlags.Hanging)) !=
                //      Objects.PlacementFlags.None && (m_Snap & Snap.Upright) != Snap.None) ||
                //     (m_EditorMode && m_MovingObjectData.HasComponent(m_Prefab)))) {
                //    bestSnapPosition.m_Elevation -= componentData7.m_Bounds.min.y;
                //    bestSnapPosition.m_Position.y =
                //        bestSnapPosition.m_Position.y - componentData7.m_Bounds.min.y;
                // }

                // if (m_StackData.TryGetComponent(m_Prefab, out StackData componentData9) &&
                //    componentData9.m_Direction == StackDirection.Up) {
                //    var num6 = componentData9.m_FirstBounds.max +
                //               (MathUtils.Size(componentData9.m_MiddleBounds) * 2f) -
                //               componentData9.m_LastBounds.min;
                //    bestSnapPosition.m_Elevation += num6;
                //    bestSnapPosition.m_Position.y = bestSnapPosition.m_Position.y + num6;
                // }

                // m_ControlPoints[0] = bestSnapPosition;
            }

            // private void FindLoweredParent(ref ControlPoint controlPoint) {
            //    var loweredParentIterator = default(LoweredParentIterator);
            //    loweredParentIterator.m_Result = controlPoint;
            //    loweredParentIterator.m_Position = controlPoint.m_HitPosition;
            //    loweredParentIterator.m_EdgeData = m_EdgeData;
            //    loweredParentIterator.m_NodeData = m_NodeData;
            //    loweredParentIterator.m_OrphanData = m_OrphanData;
            //    loweredParentIterator.m_CurveData = m_CurveData;
            //    loweredParentIterator.m_CompositionData = m_CompositionData;
            //    loweredParentIterator.m_EdgeGeometryData = m_EdgeGeometryData;
            //    loweredParentIterator.m_StartNodeGeometryData = m_StartNodeGeometryData;
            //    loweredParentIterator.m_EndNodeGeometryData = m_EndNodeGeometryData;
            //    loweredParentIterator.m_PrefabCompositionData = m_PrefabCompositionData;
            //    var iterator = loweredParentIterator;
            //    m_NetSearchTree.Iterate<LoweredParentIterator>(ref iterator, 0);
            //    controlPoint = iterator.m_Result;
            // }

            // private void FindOriginalObject(
            //    ref ControlPoint bestSnapPosition,
            //    ControlPoint controlPoint) {
            //    var originalObjectIterator = default(OriginalObjectIterator);
            //    originalObjectIterator.m_Parent = bestSnapPosition.m_OriginalEntity;
            //    originalObjectIterator.m_BestDistance = float.MaxValue;
            //    originalObjectIterator.m_EditorMode = m_EditorMode;
            //    originalObjectIterator.m_OwnerData = m_OwnerData;
            //    originalObjectIterator.m_AttachedData = m_AttachedData;
            //    originalObjectIterator.m_PrefabRefData = m_PrefabRefData;
            //    originalObjectIterator.m_NetObjectData = m_NetObjectData;
            //    originalObjectIterator.m_TransportStopData = m_TransportStopData;
            //    var iterator = originalObjectIterator;
            //    iterator.m_Bounds = m_ObjectGeometryData.TryGetComponent(m_Prefab, out ObjectGeometryData componentData)
            //        ? ObjectUtils.CalculateBounds(
            //            bestSnapPosition.m_Position,
            //            bestSnapPosition.m_Rotation, componentData)
            //        : new Bounds3(
            //            bestSnapPosition.m_Position - 1f,
            //            bestSnapPosition.m_Position + 1f);
            //    if (m_TransportStopData.TryGetComponent(m_Prefab, out TransportStopData componentData2)) {
            //        iterator.m_TransportStopData1 = componentData2;
            //    }

            // m_ObjectSearchTree.Iterate<OriginalObjectIterator>(ref iterator, 0);
            //    if (iterator.m_Result != Entity.Null) {
            //        bestSnapPosition.m_OriginalEntity = iterator.m_Result;
            //    }
            // }

            // private void HandleWorldSize(
            //    ref ControlPoint bestSnapPosition,
            //    ControlPoint controlPoint) {
            //    var bounds = TerrainUtils.GetBounds(ref m_TerrainHeightData);
            //    bool2 @bool = false;
            //    float2 @float = 0f;
            //    var bounds2 = new Bounds3(controlPoint.m_HitPosition, controlPoint.m_HitPosition);
            //    if (m_ObjectGeometryData.TryGetComponent(m_Prefab, out ObjectGeometryData componentData)) {
            //        bounds2 = ObjectUtils.CalculateBounds(
            //            controlPoint.m_HitPosition,
            //            controlPoint.m_Rotation, componentData);
            //    }

            // if (bounds2.min.x < bounds.min.x) {
            //        @bool.x = true;
            //        @float.x = bounds.min.x;
            //    } else if (bounds2.max.x > bounds.max.x) {
            //        @bool.x = true;
            //        @float.x = bounds.max.x;
            //    }

            // if (bounds2.min.z < bounds.min.z) {
            //        @bool.y = true;
            //        @float.y = bounds.min.z;
            //    } else if (bounds2.max.z > bounds.max.z) {
            //        @bool.y = true;
            //        @float.y = bounds.max.z;
            //    }

            // if (!math.any(@bool)) {
            //        return;
            //    }

            // var snapPosition = controlPoint;
            //    snapPosition.m_OriginalEntity = Entity.Null;
            //    snapPosition.m_Direction = new float2(0f, 1f);
            //    snapPosition.m_Position.xz =
            //        math.select(controlPoint.m_HitPosition.xz, @float, @bool);
            //    snapPosition.m_Position.y = controlPoint.m_HitPosition.y;
            //    snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(2f, 1f,
            //        controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz,
            //        snapPosition.m_Direction);
            //    snapPosition.m_Rotation = quaternion.LookRotationSafe(
            //        new float3 {
            //            xz = math.sign(@float)
            //        }, math.up());
            //    AddSnapPosition(ref bestSnapPosition, snapPosition);
            // }

            // public static void AlignRotation(ref quaternion rotation, quaternion parentRotation,
            //    bool zAxis) {
            //    if (zAxis) {
            //        var @float = math.rotate(rotation, new float3(0f, 0f, 1f));
            //        var up = math.rotate(parentRotation, new float3(0f, 1f, 0f));
            //        var a = quaternion.LookRotationSafe(@float, up);
            //        var q = rotation;
            //        var num = float.MaxValue;
            //        for (var i = 0; i < 8; i++) {
            //            var quaternion = math.mul(a, quaternion.RotateZ(i * 0.7853982f));
            //            var num2 = MathUtils.RotationAngle(rotation, quaternion);
            //            if (num2 < num) {
            //                q = quaternion;
            //                num = num2;
            //            }
            //        }

            // rotation = math.normalizesafe(q, quaternion.identity);
            //        return;
            //    }

            // var float2 = math.rotate(rotation, new float3(0f, 1f, 0f));
            //    var up2 = math.rotate(parentRotation, new float3(1f, 0f, 0f));
            //    var a2 = math.mul(
            //        quaternion.LookRotationSafe(float2, up2),
            //        quaternion.RotateX(1.5707964f));
            //    var q2 = rotation;
            //    var num3 = float.MaxValue;
            //    for (var j = 0; j < 8; j++) {
            //        var quaternion2 = math.mul(a2, quaternion.RotateY(j * 0.7853982f));
            //        var num4 = MathUtils.RotationAngle(rotation, quaternion2);
            //        if (num4 < num3) {
            //            q2 = quaternion2;
            //            num3 = num4;
            //        }
            //    }

            // rotation = math.normalizesafe(q2, quaternion.identity);
            // }

            // private void AlignObject(ref ControlPoint controlPoint, ref quaternion parentRotation,
            //    bool alignRotation) {
            //    var placeableObjectData = default(PlaceableObjectData);
            //    if (m_PlaceableObjectData.HasComponent(m_Prefab)) {
            //        placeableObjectData = m_PlaceableObjectData[m_Prefab];
            //    }

            // if ((placeableObjectData.m_Flags & Objects.PlacementFlags.Hanging) !=
            //        Objects.PlacementFlags.None) {
            //        var objectGeometryData = m_ObjectGeometryData[m_Prefab];
            //        controlPoint.m_Position.y =
            //            controlPoint.m_Position.y - objectGeometryData.m_Bounds.max.y;
            //    }

            // parentRotation = quaternion.identity;
            //    if (m_TransformData.HasComponent(controlPoint.m_OriginalEntity)) {
            //        var entity = controlPoint.m_OriginalEntity;
            //        var prefabRef = m_PrefabRefData[entity];
            //        parentRotation = m_TransformData[entity].m_Rotation;
            //        while (m_OwnerData.HasComponent(entity) &&
            //               !m_BuildingData.HasComponent(prefabRef.m_Prefab)) {
            //            entity = m_OwnerData[entity].m_Owner;
            //            prefabRef = m_PrefabRefData[entity];
            //            if (m_TransformData.HasComponent(entity)) {
            //                parentRotation = m_TransformData[entity].m_Rotation;
            //            }
            //        }
            //    }

            // if ((placeableObjectData.m_Flags & Objects.PlacementFlags.Wall) !=
            //        Objects.PlacementFlags.None) {
            //        var @float = math.forward(controlPoint.m_Rotation);
            //        var value = controlPoint.m_HitDirection;
            //        value.y = math.select(value.y, 0f, (m_Snap & Snap.Upright) > Snap.None);
            //        if (!MathUtils.TryNormalize(ref value)) {
            //            value = @float;
            //            value.y = math.select(value.y, 0f, (m_Snap & Snap.Upright) > Snap.None);
            //            if (!MathUtils.TryNormalize(ref value)) {
            //                value = new float3(0f, 0f, 1f);
            //            }
            //        }

            // var value2 = math.cross(@float, value);
            //        if (MathUtils.TryNormalize(ref value2)) {
            //            var angle = math.acos(math.clamp(math.dot(@float, value), -1f, 1f));
            //            controlPoint.m_Rotation = math.normalizesafe(
            //                math.mul(quaternion.AxisAngle(value2, angle), controlPoint.m_Rotation),
            //                quaternion.identity);
            //            if (alignRotation) {
            //                AlignRotation(ref controlPoint.m_Rotation, parentRotation, true);
            //            }
            //        }

            // controlPoint.m_Position += math.forward(controlPoint.m_Rotation) *
            //                                   placeableObjectData.m_PlacementOffset.z;
            //        return;
            //    }

            // var float2 = math.rotate(controlPoint.m_Rotation, new float3(0f, 1f, 0f));
            //    var hitDirection = controlPoint.m_HitDirection;
            //    hitDirection = math.select(hitDirection, new float3(0f, 1f, 0f),
            //        (m_Snap & Snap.Upright) > Snap.None);
            //    if (!MathUtils.TryNormalize(ref hitDirection)) {
            //        hitDirection = float2;
            //    }

            // var value3 = math.cross(float2, hitDirection);
            //    if (MathUtils.TryNormalize(ref value3)) {
            //        var angle2 = math.acos(math.clamp(math.dot(float2, hitDirection), -1f, 1f));
            //        controlPoint.m_Rotation = math.normalizesafe(
            //            math.mul(quaternion.AxisAngle(value3, angle2), controlPoint.m_Rotation),
            //            quaternion.identity);
            //        if (alignRotation) {
            //            AlignRotation(ref controlPoint.m_Rotation, parentRotation, false);
            //        }
            //    }
            // }

            // private void CalculateHeight(ref ControlPoint controlPoint, float waterSurfaceHeight) {
            //    if (m_PlaceableObjectData.HasComponent(m_Prefab)) {
            //        var placeableObjectData = m_PlaceableObjectData[m_Prefab];
            //        if (m_SubObjects.HasBuffer(controlPoint.m_OriginalEntity)) {
            //            controlPoint.m_Position.y = controlPoint.m_Position.y +
            //                                        placeableObjectData.m_PlacementOffset.y;
            //            return;
            //        }

            // float num;
            //        if ((placeableObjectData.m_Flags & Objects.PlacementFlags.RoadSide) !=
            //            Objects.PlacementFlags.None && m_BuildingData.HasComponent(m_Prefab)) {
            //            var buildingData = m_BuildingData[m_Prefab];
            //            var worldPosition = BuildingUtils.CalculateFrontPosition(
            //                new Objects.Transform(controlPoint.m_Position, controlPoint.m_Rotation),
            //                buildingData.m_LotSize.y);
            //            num = TerrainUtils.SampleHeight(ref m_TerrainHeightData, worldPosition);
            //        } else {
            //            num = TerrainUtils.SampleHeight(
            //                ref m_TerrainHeightData,
            //                controlPoint.m_Position);
            //        }

            // if ((placeableObjectData.m_Flags & Objects.PlacementFlags.Hovering) !=
            //            Objects.PlacementFlags.None) {
            //            var num2 = WaterUtils.SampleHeight(
            //                ref m_WaterSurfaceData,
            //                ref m_TerrainHeightData, controlPoint.m_Position);
            //            num2 += placeableObjectData.m_PlacementOffset.y;
            //            controlPoint.m_Elevation = math.max(0f, num2 - num);
            //            num = math.max(num, num2);
            //        } else if ((placeableObjectData.m_Flags & (Objects.PlacementFlags.Shoreline |
            //                                                   Objects.PlacementFlags.Floating)) ==
            //                   Objects.PlacementFlags.None) {
            //            num += placeableObjectData.m_PlacementOffset.y;
            //        } else {
            //            var num3 = WaterUtils.SampleHeight(
            //                ref m_WaterSurfaceData,
            //                ref m_TerrainHeightData, controlPoint.m_Position, out float waterDepth);
            //            if (waterDepth >= 0.2f) {
            //                num3 += placeableObjectData.m_PlacementOffset.y;
            //                if ((placeableObjectData.m_Flags & Objects.PlacementFlags.Floating) !=
            //                    Objects.PlacementFlags.None) {
            //                    controlPoint.m_Elevation = math.max(0f, num3 - num);
            //                }

            // num = math.max(num, num3);
            //            }
            //        }

            // if ((m_Snap & Snap.Shoreline) != Snap.None) {
            //            num = math.max(
            //                num,
            //                waterSurfaceHeight + placeableObjectData.m_PlacementOffset.y);
            //        }

            // controlPoint.m_Position.y = num;
            //    }
            // }

            // private void SnapSurface(ControlPoint controlPoint, ref ControlPoint bestPosition,
            //    Entity entity, int parentMesh) {
            //    var transform = m_TransformData[entity];
            //    var snapPosition = controlPoint;
            //    snapPosition.m_OriginalEntity = entity;
            //    snapPosition.m_ElementIndex.x = parentMesh;
            //    snapPosition.m_Position = controlPoint.m_HitPosition;
            //    snapPosition.m_Direction = math.forward(transform.m_Rotation).xz;
            //    snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f,
            //        controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz,
            //        snapPosition.m_Direction);
            //    AddSnapPosition(ref bestPosition, snapPosition);
            // }

            // private void SnapNode(ControlPoint controlPoint, ref ControlPoint bestPosition,
            //    Entity entity, Net.Node node) {
            //    var bounds = new Bounds1(float.MaxValue, float.MinValue);
            //    var dynamicBuffer = m_ConnectedEdges[entity];
            //    for (var i = 0; i < dynamicBuffer.Length; i++) {
            //        var edge = dynamicBuffer[i].m_Edge;
            //        var edge2 = m_EdgeData[edge];
            //        if (edge2.m_Start == entity) {
            //            var composition = m_CompositionData[edge];
            //            var netCompositionData = m_PrefabCompositionData[composition.m_StartNode];
            //            bounds |= netCompositionData.m_SurfaceHeight;
            //        } else if (edge2.m_End == entity) {
            //            var composition2 = m_CompositionData[edge];
            //            var netCompositionData2 = m_PrefabCompositionData[composition2.m_EndNode];
            //            bounds |= netCompositionData2.m_SurfaceHeight;
            //        }
            //    }

            // var snapPosition = controlPoint;
            //    snapPosition.m_OriginalEntity = entity;
            //    snapPosition.m_Position = node.m_Position;
            //    if (bounds.min < 3.4028235E+38f) {
            //        snapPosition.m_Position.y = snapPosition.m_Position.y + bounds.min;
            //    }

            // snapPosition.m_Direction =
            //        math.normalizesafe(math.forward(node.m_Rotation), default).xz;
            //    snapPosition.m_Rotation = node.m_Rotation;
            //    snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f,
            //        controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz,
            //        snapPosition.m_Direction);
            //    AddSnapPosition(ref bestPosition, snapPosition);
            // }

            // private void SnapShoreline(ControlPoint controlPoint, ref ControlPoint bestPosition,
            //    ref float waterSurfaceHeight, float radius, float3 offset) {
            //    var x = (int2)math.floor(WaterUtils.ToSurfaceSpace(
            //        ref m_WaterSurfaceData,
            //        controlPoint.m_HitPosition - radius).xz);
            //    var x2 = (int2)math.ceil(WaterUtils.ToSurfaceSpace(
            //        ref m_WaterSurfaceData,
            //        controlPoint.m_HitPosition + radius).xz);
            //    x = math.max(x, default);
            //    x2 = math.min(x2, m_WaterSurfaceData.resolution.xz - 1);
            //    var @float = default(float3);
            //    var float2 = default(float3);
            //    var float3 = default(float2);
            //    for (var i = x.y; i <= x2.y; i++) {
            //        for (var j = x.x; j <= x2.x; j++) {
            //            var worldPosition =
            //                WaterUtils.GetWorldPosition(ref m_WaterSurfaceData, new int2(j, i));
            //            if (worldPosition.y > 0.2f) {
            //                var num =
            //                    TerrainUtils.SampleHeight(ref m_TerrainHeightData, worldPosition) +
            //                    worldPosition.y;
            //                var num2 = math.max(
            //                    0f,
            //                    (radius * radius) - math.distancesq(
            //                        worldPosition.xz,
            //                        controlPoint.m_HitPosition.xz));
            //                worldPosition.y = (worldPosition.y - 0.2f) * num2;
            //                worldPosition.xz *= worldPosition.y;
            //                float2 += worldPosition;
            //                num *= num2;
            //                float3 += new float2(num, num2);
            //            } else if (worldPosition.y < 0.2f) {
            //                var num3 = math.max(
            //                    0f,
            //                    (radius * radius) - math.distancesq(
            //                        worldPosition.xz,
            //                        controlPoint.m_HitPosition.xz));
            //                worldPosition.y = (0.2f - worldPosition.y) * num3;
            //                worldPosition.xz *= worldPosition.y;
            //                @float += worldPosition;
            //            }
            //        }
            //    }

            // if (@float.y != 0f && float2.y != 0f && float3.y != 0f) {
            //        @float /= @float.y;
            //        float2 /= float2.y;
            //        var value = default(float3);
            //        value.xz = @float.xz - float2.xz;
            //        if (MathUtils.TryNormalize(ref value)) {
            //            waterSurfaceHeight = float3.x / float3.y;
            //            bestPosition = controlPoint;
            //            bestPosition.m_Position.xz = math.lerp(float2.xz, @float.xz, 0.5f);
            //            bestPosition.m_Position.y = waterSurfaceHeight + offset.y;
            //            bestPosition.m_Position += value * offset.z;
            //            bestPosition.m_Direction = value.xz;
            //            bestPosition.m_Rotation =
            //                ToolUtils.CalculateRotation(bestPosition.m_Direction);
            //            bestPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f,
            //                controlPoint.m_HitPosition.xz, bestPosition.m_Position.xz,
            //                bestPosition.m_Direction);
            //            bestPosition.m_OriginalEntity = Entity.Null;
            //        }
            //    }
            // }

            // private void SnapSegmentAreas(ControlPoint controlPoint, ref ControlPoint bestPosition,
            //    float radius, Entity entity, Segment segment1,
            //    NetCompositionData prefabCompositionData1, DynamicBuffer<NetCompositionArea> areas1) {
            //    for (var i = 0; i < areas1.Length; i++) {
            //        var netCompositionArea = areas1[i];
            //        if ((netCompositionArea.m_Flags & NetAreaFlags.Buildable) != 0) {
            //            var num = netCompositionArea.m_Width * 0.51f;
            //            if (radius < num) {
            //                var curve = MathUtils.Lerp(segment1.m_Left, segment1.m_Right,
            //                    (netCompositionArea.m_Position.x / prefabCompositionData1.m_Width) +
            //                    0.5f);
            //                MathUtils.Distance(curve.xz, controlPoint.m_HitPosition.xz, out float t);
            //                var snapPosition = controlPoint;
            //                snapPosition.m_OriginalEntity = entity;
            //                snapPosition.m_Position = MathUtils.Position(curve, t);
            //                snapPosition.m_Direction =
            //                    math.normalizesafe(MathUtils.Tangent(curve, t).xz, default);
            //                snapPosition.m_Direction = (netCompositionArea.m_Flags & NetAreaFlags.Invert) !=
            //                    0
            //                    ? MathUtils.Right(snapPosition.m_Direction)
            //                    : MathUtils.Left(snapPosition.m_Direction);
            //                var @float = MathUtils.Position(
            //                    MathUtils.Lerp(segment1.m_Left, segment1.m_Right,
            //                        (netCompositionArea.m_SnapPosition.x /
            //                        prefabCompositionData1.m_Width) + 0.5f), t);
            //                var maxLength = math.max(
            //                    0f,
            //                    math.min(
            //                        netCompositionArea.m_Width * 0.5f,
            //                        math.abs(netCompositionArea.m_SnapPosition.x -
            //                                 netCompositionArea.m_Position.x) +
            //                        (netCompositionArea.m_SnapWidth * 0.5f)) - radius);
            //                var maxLength2 = math.max(
            //                    0f,
            //                    (netCompositionArea.m_SnapWidth * 0.5f) - radius);
            //                snapPosition.m_Position.xz = snapPosition.m_Position.xz +
            //                                             MathUtils.ClampLength(
            //                                                 @float.xz - snapPosition.m_Position.xz,
            //                                                 maxLength);
            //                snapPosition.m_Position.xz = snapPosition.m_Position.xz +
            //                                             MathUtils.ClampLength(
            //                                                 controlPoint.m_HitPosition.xz -
            //                                                 snapPosition.m_Position.xz,
            //                                                 maxLength2);
            //                snapPosition.m_Position.y = snapPosition.m_Position.y +
            //                                            netCompositionArea.m_Position.y;
            //                snapPosition.m_Rotation =
            //                    ToolUtils.CalculateRotation(snapPosition.m_Direction);
            //                snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f,
            //                    controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz,
            //                    snapPosition.m_Direction);
            //                AddSnapPosition(ref bestPosition, snapPosition);
            //            }
            //        }
            //    }
            // }

            // private static Bounds3 SetHeightRange(Bounds3 bounds, Bounds1 heightRange) {
            //    bounds.min.y = bounds.min.y + heightRange.min;
            //    bounds.max.y = bounds.max.y + heightRange.max;
            //    return bounds;
            // }

            // private static void CheckSnapLine(
            //    BuildingData buildingData,
            //    Objects.Transform ownerTransformData, ControlPoint controlPoint,
            //    ref ControlPoint bestPosition, Line2 line, int maxOffset, float angle,
            //    bool forceSnap) {
            //    MathUtils.Distance(line, controlPoint.m_Position.xz, out float t);
            //    var num = math.select(0f, 4f,
            //        ((buildingData.m_LotSize.x - buildingData.m_LotSize.y) & 1) != 0);
            //    var num2 =
            //        math.min(
            //            (2 * maxOffset) - buildingData.m_LotSize.y - buildingData.m_LotSize.x,
            //            buildingData.m_LotSize.y - buildingData.m_LotSize.x) * 4f;
            //    var num3 = math.distance(line.a, line.b);
            //    t *= num3;
            //    t = MathUtils.Snap(t + num, 8f) - num;
            //    t = math.clamp(t, -num2, num3 + num2);
            //    var snapPosition = controlPoint;
            //    snapPosition.m_OriginalEntity = Entity.Null;
            //    snapPosition.m_Position.y = ownerTransformData.m_Position.y;
            //    snapPosition.m_Position.xz = MathUtils.Position(line, t / num3);
            //    snapPosition.m_Direction = math
            //        .mul(
            //            math.mul(ownerTransformData.m_Rotation, quaternion.RotateY(angle)),
            //            new float3(0f, 0f, 1f)).xz;
            //    snapPosition.m_Rotation = ToolUtils.CalculateRotation(snapPosition.m_Direction);
            //    var level = math.select(0f, 1f, forceSnap);
            //    snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(level, 1f,
            //        controlPoint.m_HitPosition.xz * 0.5f, snapPosition.m_Position.xz * 0.5f,
            //        snapPosition.m_Direction);
            //    AddSnapPosition(ref bestPosition, snapPosition);
            // }

            // private static void AddSnapPosition(
            //    ref ControlPoint bestSnapPosition,
            //    ControlPoint snapPosition) {
            //    if (ToolUtils.CompareSnapPriority(
            //        snapPosition.m_SnapPriority,
            //        bestSnapPosition.m_SnapPriority)) {
            //        bestSnapPosition = snapPosition;
            //    }
            // }

            // private struct LoweredParentIterator :
            //    INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>,
            //    IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            //    public bool Intersect(QuadTreeBoundsXZ bounds) {
            //        return MathUtils.Intersect(bounds.m_Bounds.xz, m_Position.xz);
            //    }

            // public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
            //        if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Position.xz)) {
            //            return;
            //        }

            // if (m_EdgeGeometryData.HasComponent(entity)) {
            //            CheckEdge(entity);
            //            return;
            //        }

            // if (m_OrphanData.HasComponent(entity)) {
            //            CheckNode(entity);
            //        }
            //    }

            // private void CheckNode(Entity entity) {
            //        var node = m_NodeData[entity];
            //        var orphan = m_OrphanData[entity];
            //        var netCompositionData = m_PrefabCompositionData[orphan.m_Composition];
            //        if ((netCompositionData.m_State & CompositionState.Marker) ==
            //            0 &&
            //            ((netCompositionData.m_Flags.m_Left | netCompositionData.m_Flags.m_Right) &
            //             CompositionFlags.Side.Lowered) != 0U) {
            //            var position = node.m_Position;
            //            position.y += netCompositionData.m_SurfaceHeight.max;
            //            if (math.distance(m_Position.xz, position.xz) <=
            //                netCompositionData.m_Width * 0.5f) {
            //                m_Result.m_OriginalEntity = entity;
            //                m_Result.m_Position = node.m_Position;
            //                m_Result.m_HitPosition = m_Position;
            //                m_Result.m_HitPosition.y = position.y;
            //                m_Result.m_HitDirection = default;
            //            }
            //        }
            //    }

            // private void CheckEdge(Entity entity) {
            //        var edgeGeometry = m_EdgeGeometryData[entity];
            //        var geometry = m_StartNodeGeometryData[entity].m_Geometry;
            //        var geometry2 = m_EndNodeGeometryData[entity].m_Geometry;
            //        bool3 x;
            //        x.x = MathUtils.Intersect(edgeGeometry.m_Bounds.xz, m_Position.xz);
            //        x.y = MathUtils.Intersect(geometry.m_Bounds.xz, m_Position.xz);
            //        x.z = MathUtils.Intersect(geometry2.m_Bounds.xz, m_Position.xz);
            //        if (!math.any(x)) {
            //            return;
            //        }

            // var composition = m_CompositionData[entity];
            //        var edge = m_EdgeData[entity];
            //        var curve = m_CurveData[entity];
            //        if (x.x) {
            //            var prefabCompositionData = m_PrefabCompositionData[composition.m_Edge];
            //            if ((prefabCompositionData.m_State & CompositionState.Marker) ==
            //                0 &&
            //                ((prefabCompositionData.m_Flags.m_Left |
            //                  prefabCompositionData.m_Flags.m_Right) &
            //                 CompositionFlags.Side.Lowered) != 0U) {
            //                CheckSegment(entity, edgeGeometry.m_Start, curve.m_Bezier,
            //                    prefabCompositionData);
            //                CheckSegment(entity, edgeGeometry.m_End, curve.m_Bezier,
            //                    prefabCompositionData);
            //            }
            //        }

            // if (x.y) {
            //            var prefabCompositionData2 =
            //                m_PrefabCompositionData[composition.m_StartNode];
            //            if ((prefabCompositionData2.m_State & CompositionState.Marker) ==
            //                0 &&
            //                ((prefabCompositionData2.m_Flags.m_Left |
            //                  prefabCompositionData2.m_Flags.m_Right) &
            //                 CompositionFlags.Side.Lowered) != 0U) {
            //                if (geometry.m_MiddleRadius > 0f) {
            //                    CheckSegment(edge.m_Start, geometry.m_Left, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                    var right = geometry.m_Right;
            //                    var right2 = geometry.m_Right;
            //                    right.m_Right = MathUtils.Lerp(
            //                        geometry.m_Right.m_Left,
            //                        geometry.m_Right.m_Right, 0.5f);
            //                    right2.m_Left = MathUtils.Lerp(
            //                        geometry.m_Right.m_Left,
            //                        geometry.m_Right.m_Right, 0.5f);
            //                    right.m_Right.d = geometry.m_Middle.d;
            //                    right2.m_Left.d = geometry.m_Middle.d;
            //                    CheckSegment(edge.m_Start, right, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                    CheckSegment(edge.m_Start, right2, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                } else {
            //                    var left = geometry.m_Left;
            //                    var right3 = geometry.m_Right;
            //                    CheckSegment(edge.m_Start, left, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                    CheckSegment(edge.m_Start, right3, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                    left.m_Right = geometry.m_Middle;
            //                    right3.m_Left = geometry.m_Middle;
            //                    CheckSegment(edge.m_Start, left, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                    CheckSegment(edge.m_Start, right3, curve.m_Bezier,
            //                        prefabCompositionData2);
            //                }
            //            }
            //        }

            // if (x.z) {
            //            var prefabCompositionData3 = m_PrefabCompositionData[composition.m_EndNode];
            //            if ((prefabCompositionData3.m_State & CompositionState.Marker) ==
            //                0 &&
            //                ((prefabCompositionData3.m_Flags.m_Left |
            //                  prefabCompositionData3.m_Flags.m_Right) &
            //                 CompositionFlags.Side.Lowered) != 0U) {
            //                if (geometry2.m_MiddleRadius > 0f) {
            //                    CheckSegment(edge.m_End, geometry2.m_Left, curve.m_Bezier,
            //                        prefabCompositionData3);
            //                    var right4 = geometry2.m_Right;
            //                    var right5 = geometry2.m_Right;
            //                    right4.m_Right = MathUtils.Lerp(
            //                        geometry2.m_Right.m_Left,
            //                        geometry2.m_Right.m_Right, 0.5f);
            //                    right4.m_Right.d = geometry2.m_Middle.d;
            //                    right5.m_Left = right4.m_Right;
            //                    CheckSegment(edge.m_End, right4, curve.m_Bezier,
            //                        prefabCompositionData3);
            //                    CheckSegment(edge.m_End, right5, curve.m_Bezier,
            //                        prefabCompositionData3);
            //                    return;
            //                }

            // var left2 = geometry2.m_Left;
            //                var right6 = geometry2.m_Right;
            //                CheckSegment(edge.m_End, left2, curve.m_Bezier, prefabCompositionData3);
            //                CheckSegment(edge.m_End, right6, curve.m_Bezier,
            //                    prefabCompositionData3);
            //                left2.m_Right = geometry2.m_Middle;
            //                right6.m_Left = geometry2.m_Middle;
            //                CheckSegment(edge.m_End, left2, curve.m_Bezier, prefabCompositionData3);
            //                CheckSegment(edge.m_End, right6, curve.m_Bezier,
            //                    prefabCompositionData3);
            //            }
            //        }
            //    }

            // private void CheckSegment(Entity entity, Segment segment, Bezier4x3 curve,
            //        NetCompositionData prefabCompositionData) {
            //        var a = segment.m_Left.a;
            //        var @float = segment.m_Right.a;
            //        for (var i = 1; i <= 8; i++) {
            //            var t = i / 8f;
            //            var float2 = MathUtils.Position(segment.m_Left, t);
            //            var float3 = MathUtils.Position(segment.m_Right, t);
            //            var triangle = new Triangle3(a, @float, float2);
            //            var triangle2 = new Triangle3(float3, float2, @float);
            //            if (MathUtils.Intersect(triangle.xz, m_Position.xz, out float2 t2)) {
            //                var position = m_Position;
            //                position.y = MathUtils.Position(triangle.y, t2) +
            //                             prefabCompositionData.m_SurfaceHeight.max;
            //                MathUtils.Distance(curve.xz, position.xz, out float t3);
            //                m_Result.m_OriginalEntity = entity;
            //                m_Result.m_Position = MathUtils.Position(curve, t3);
            //                m_Result.m_HitPosition = position;
            //                m_Result.m_HitDirection = default;
            //                m_Result.m_CurvePosition = t3;
            //            } else if (MathUtils.Intersect(triangle2.xz, m_Position.xz, out t2)) {
            //                var position2 = m_Position;
            //                position2.y = MathUtils.Position(triangle2.y, t2) +
            //                              prefabCompositionData.m_SurfaceHeight.max;
            //                MathUtils.Distance(curve.xz, position2.xz, out float t4);
            //                m_Result.m_OriginalEntity = entity;
            //                m_Result.m_Position = MathUtils.Position(curve, t4);
            //                m_Result.m_HitPosition = position2;
            //                m_Result.m_HitDirection = default;
            //                m_Result.m_CurvePosition = t4;
            //            }

            // a = float2;
            //            @float = float3;
            //        }
            //    }

            // public ControlPoint m_Result;

            // public float3 m_Position;

            // public ComponentLookup<Edge> m_EdgeData;

            // public ComponentLookup<Net.Node> m_NodeData;

            // public ComponentLookup<Orphan> m_OrphanData;

            // public ComponentLookup<Curve> m_CurveData;

            // public ComponentLookup<Composition> m_CompositionData;

            // public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            // public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;

            // public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;

            // public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
            // }

            // private struct OriginalObjectIterator :
            //    INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>,
            //    IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            //    public bool Intersect(QuadTreeBoundsXZ bounds) {
            //        return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
            //    }

            // public void Iterate(QuadTreeBoundsXZ bounds, Entity item) {
            //        if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds)) {
            //            return;
            //        }

            // if (!m_AttachedData.HasComponent(item) ||
            //            (!m_EditorMode && m_OwnerData.HasComponent(item))) {
            //            return;
            //        }

            // if (m_AttachedData[item].m_Parent != m_Parent) {
            //            return;
            //        }

            // var prefabRef = m_PrefabRefData[item];
            //        if (!m_NetObjectData.HasComponent(prefabRef.m_Prefab)) {
            //            return;
            //        }

            // var transportStopData = default(TransportStopData);
            //        if (m_TransportStopData.HasComponent(prefabRef.m_Prefab)) {
            //            transportStopData = m_TransportStopData[prefabRef.m_Prefab];
            //        }

            // if (m_TransportStopData1.m_TransportType !=
            //            transportStopData.m_TransportType) {
            //            return;
            //        }

            // var num = math.distance(
            //            MathUtils.Center(m_Bounds),
            //            MathUtils.Center(bounds.m_Bounds));
            //        if (num < m_BestDistance) {
            //            m_Result = item;
            //            m_BestDistance = num;
            //        }
            //    }

            // public Entity m_Parent;

            // public Entity m_Result;

            // public Bounds3 m_Bounds;

            // public float m_BestDistance;

            // public bool m_EditorMode;

            // public TransportStopData m_TransportStopData1;

            // public ComponentLookup<Owner> m_OwnerData;

            // public ComponentLookup<Attached> m_AttachedData;

            // public ComponentLookup<PrefabRef> m_PrefabRefData;

            // public ComponentLookup<NetObjectData> m_NetObjectData;

            // public ComponentLookup<TransportStopData> m_TransportStopData;
            // }

            // private struct ParentObjectIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>,
            //    IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            //    public bool Intersect(QuadTreeBoundsXZ bounds) {
            //        return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
            //    }

            // public void Iterate(QuadTreeBoundsXZ bounds, Entity item) {
            //        if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz)) {
            //            return;
            //        }

            // var prefabRef = m_PrefabRefData[item];
            //        var flag = m_BuildingData.HasComponent(prefabRef.m_Prefab);
            //        var flag2 = m_AssetStampData.HasComponent(prefabRef.m_Prefab);
            //        if (m_IsBuilding && !flag2) {
            //            return;
            //        }

            // var num = m_BestOverlap;
            //        if (flag || flag2) {
            //            var transform = m_TransformData[item];
            //            var objectGeometryData = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
            //            var @float = MathUtils.Center(bounds.m_Bounds);
            //            if ((m_PrefabObjectGeometryData1.m_Flags &
            //                 Objects.GeometryFlags.Circular) != Objects.GeometryFlags.None) {
            //                var circle =
            //                    new Circle2(
            //                        (m_PrefabObjectGeometryData1.m_Size.x * 0.5f) - 0.01f,
            //                        (m_ControlPoint.m_Position - @float).xz);
            //                if ((objectGeometryData.m_Flags & Objects.GeometryFlags.Circular) !=
            //                    Objects.GeometryFlags.None) {
            //                    var circle2 =
            //                        new Circle2(
            //                            (objectGeometryData.m_Size.x * 0.5f) - 0.01f,
            //                            (transform.m_Position - @float).xz);
            //                    if (MathUtils.Intersect(circle, circle2)) {
            //                        num = math.distance(
            //                            new float3 {
            //                                xz = @float.xz + MathUtils.Center(MathUtils.Bounds(circle) &
            //                                MathUtils.Bounds(circle2)),
            //                                y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y)
            //                            }, m_ControlPoint.m_Position);
            //                    }
            //                } else if (MathUtils.Intersect(
            //                               ObjectUtils.CalculateBaseCorners(
            //                                       transform.m_Position - @float,
            //                                       transform.m_Rotation,
            //                                       MathUtils.Expand(
            //                                           objectGeometryData.m_Bounds,
            //                                           -0.01f))
            //                                   .xz, circle, out Bounds2 intersection)) {
            //                    num = math.distance(
            //                        new float3 {
            //                            xz = @float.xz + MathUtils.Center(intersection),
            //                            y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y)
            //                        }, m_ControlPoint.m_Position);
            //                }
            //            } else {
            //                var xz = ObjectUtils.CalculateBaseCorners(
            //                    m_ControlPoint.m_Position - @float, m_ControlPoint.m_Rotation,
            //                    MathUtils.Expand(m_PrefabObjectGeometryData1.m_Bounds, -0.01f)).xz;
            //                if ((objectGeometryData.m_Flags & Objects.GeometryFlags.Circular) !=
            //                    Objects.GeometryFlags.None) {
            //                    var circle3 =
            //                        new Circle2(
            //                            (objectGeometryData.m_Size.x * 0.5f) - 0.01f,
            //                            (transform.m_Position - @float).xz);
            //                    if (MathUtils.Intersect(xz, circle3, out Bounds2 intersection2)) {
            //                        num = math.distance(
            //                            new float3 {
            //                                xz = @float.xz + MathUtils.Center(intersection2),
            //                                y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y)
            //                            }, m_ControlPoint.m_Position);
            //                    }
            //                } else {
            //                    var xz2 = ObjectUtils
            //                        .CalculateBaseCorners(
            //                            transform.m_Position - @float,
            //                            transform.m_Rotation,
            //                            MathUtils.Expand(objectGeometryData.m_Bounds, -0.01f)).xz;
            //                    if (MathUtils.Intersect(xz, xz2, out Bounds2 intersection3)) {
            //                        num = math.distance(
            //                            new float3 {
            //                                xz = @float.xz + MathUtils.Center(intersection3),
            //                                y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y)
            //                            }, m_ControlPoint.m_Position);
            //                    }
            //                }
            //            }
            //        } else {
            //            if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds)) {
            //                return;
            //            }

            // if (!m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab)) {
            //                return;
            //            }

            // var transform2 = m_TransformData[item];
            //            var objectGeometryData2 = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
            //            var float2 = MathUtils.Center(bounds.m_Bounds);
            //            var quaternion = math.inverse(m_ControlPoint.m_Rotation);
            //            var q2 = math.inverse(transform2.m_Rotation);
            //            var float3 = math.mul(quaternion, m_ControlPoint.m_Position - float2);
            //            var float4 = math.mul(q2, transform2.m_Position - float2);
            //            if ((m_PrefabObjectGeometryData1.m_Flags &
            //                 Objects.GeometryFlags.Circular) != Objects.GeometryFlags.None) {
            //                var cylinder = default(Cylinder3);
            //                cylinder.circle =
            //                    new Circle2(
            //                        (m_PrefabObjectGeometryData1.m_Size.x * 0.5f) - 0.01f,
            //                        float3.xz);
            //                cylinder.height = new Bounds1(
            //                    0.01f,
            //                    m_PrefabObjectGeometryData1.m_Size.y - 0.01f) + float3.y;
            //                cylinder.rotation = m_ControlPoint.m_Rotation;
            //                if ((objectGeometryData2.m_Flags & Objects.GeometryFlags.Circular) !=
            //                    Objects.GeometryFlags.None) {
            //                    var cylinder2 = default(Cylinder3);
            //                    cylinder2.circle =
            //                        new Circle2(
            //                            (objectGeometryData2.m_Size.x * 0.5f) - 0.01f,
            //                            float4.xz);
            //                    cylinder2.height =
            //                        new Bounds1(0.01f, objectGeometryData2.m_Size.y - 0.01f) +
            //                        float4.y;
            //                    cylinder2.rotation = transform2.m_Rotation;
            //                    var pos = default(float3);
            //                    if (Objects.ValidationHelpers.Intersect(cylinder, cylinder2,
            //                            ref pos)) {
            //                        num = math.distance(pos, m_ControlPoint.m_Position);
            //                    }
            //                } else {
            //                    var box = default(Box3);
            //                    box.bounds = objectGeometryData2.m_Bounds + float4;
            //                    box.bounds = MathUtils.Expand(box.bounds, -0.01f);
            //                    box.rotation = transform2.m_Rotation;
            //                    if (MathUtils.Intersect(cylinder, box, out Bounds3 cylinderIntersection,
            //                            out Bounds3 boxIntersection)) {
            //                        var x5 = math.mul(
            //                            cylinder.rotation,
            //                            MathUtils.Center(cylinderIntersection));
            //                        var y = math.mul(
            //                            box.rotation,
            //                            MathUtils.Center(boxIntersection));
            //                        num = math.distance(
            //                            float2 + math.lerp(x5, y, 0.5f),
            //                            m_ControlPoint.m_Position);
            //                    }
            //                }
            //            } else {
            //                var box2 = default(Box3);
            //                box2.bounds = m_PrefabObjectGeometryData1.m_Bounds + float3;
            //                box2.bounds = MathUtils.Expand(box2.bounds, -0.01f);
            //                box2.rotation = m_ControlPoint.m_Rotation;
            //                if ((objectGeometryData2.m_Flags & Objects.GeometryFlags.Circular) !=
            //                    Objects.GeometryFlags.None) {
            //                    var cylinder3 = new Cylinder3 {
            //                        circle = new Circle2(
            //                            (objectGeometryData2.m_Size.x * 0.5f) - 0.01f, float4.xz),
            //                        height = new Bounds1(
            //                            0.01f,
            //                            objectGeometryData2.m_Size.y - 0.01f) + float4.y,
            //                        rotation = transform2.m_Rotation
            //                    };
            //                    if (MathUtils.Intersect(cylinder3, box2, out Bounds3 cylinderIntersection2,
            //                            out Bounds3 boxIntersection2)) {
            //                        var x6 = math.mul(
            //                            box2.rotation,
            //                            MathUtils.Center(boxIntersection2));
            //                        var y2 = math.mul(
            //                            cylinder3.rotation,
            //                            MathUtils.Center(cylinderIntersection2));
            //                        num = math.distance(
            //                            float2 + math.lerp(x6, y2, 0.5f),
            //                            m_ControlPoint.m_Position);
            //                    }
            //                } else {
            //                    var box3 = default(Box3);
            //                    box3.bounds = objectGeometryData2.m_Bounds + float4;
            //                    box3.bounds = MathUtils.Expand(box3.bounds, -0.01f);
            //                    box3.rotation = transform2.m_Rotation;
            //                    if (MathUtils.Intersect(box2, box3, out Bounds3 intersection4,
            //                            out Bounds3 intersection5)) {
            //                        var x7 = math.mul(
            //                            box2.rotation,
            //                            MathUtils.Center(intersection4));
            //                        var y3 = math.mul(
            //                            box3.rotation,
            //                            MathUtils.Center(intersection5));
            //                        num = math.distance(
            //                            float2 + math.lerp(x7, y3, 0.5f),
            //                            m_ControlPoint.m_Position);
            //                    }
            //                }
            //            }
            //        }

            // if (num < m_BestOverlap) {
            //            m_BestSnapPosition = m_ControlPoint;
            //            m_BestSnapPosition.m_OriginalEntity = item;
            //            m_BestSnapPosition.m_ElementIndex = new int2(-1, -1);
            //            m_BestOverlap = num;
            //        }
            //    }

            // public ControlPoint m_ControlPoint;

            // public ControlPoint m_BestSnapPosition;

            // public Bounds3 m_Bounds;

            // public float m_BestOverlap;

            // public bool m_IsBuilding;

            // public ObjectGeometryData m_PrefabObjectGeometryData1;

            // public ComponentLookup<Objects.Transform> m_TransformData;

            // public ComponentLookup<PrefabRef> m_PrefabRefData;

            // public ComponentLookup<BuildingData> m_BuildingData;

            // public ComponentLookup<AssetStampData> m_AssetStampData;

            // public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
            // }

            // private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>,
            //    IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
            //    public bool Intersect(QuadTreeBoundsXZ bounds) {
            //        return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds);
            //    }

            // public void Iterate(QuadTreeBoundsXZ bounds, Entity netEntity) {
            //        if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds)) {
            //            return;
            //        }

            // if (!m_NodeData.TryGetComponent(netEntity, out Net.Node componentData)) {
            //            return;
            //        }

            // if (m_IgnoreOwner != Entity.Null) {
            //            var entity = netEntity;
            //            while (m_OwnerData.TryGetComponent(entity, out Owner componentData2)) {
            //                if (componentData2.m_Owner == m_IgnoreOwner) {
            //                    return;
            //                }

            // entity = componentData2.m_Owner;
            //            }
            //        }

            // var flag = true;
            //        var num = float.MaxValue;
            //        var num2 = float.MaxValue;
            //        var @float = default(float3);
            //        var float2 = default(float2);
            //        var float3 = math.mul(m_Rotation, m_LocalOffset);
            //        var xz = math.mul(
            //            m_Rotation,
            //            new float3(m_LocalTangent.x, 0f, m_LocalTangent.y)).xz;
            //        var controlPoint = m_ControlPoint;
            //        controlPoint.m_OriginalEntity = Entity.Null;
            //        controlPoint.m_Direction =
            //            math.normalizesafe(math.forward(m_Rotation).xz, default);
            //        controlPoint.m_Rotation = m_Rotation;
            //        if (m_ConnectedEdges.TryGetBuffer(netEntity, out DynamicBuffer<ConnectedEdge> bufferData)) {
            //            var flag2 =
            //                (m_NetGeometryData.m_Flags & Net.GeometryFlags.StrictNodes) ==
            //                0 &&
            //                (m_RoadData.m_Flags & Prefabs.RoadFlags.EnableZoning) >
            //                0;
            //            var i = 0;
            //            while (i < bufferData.Length) {
            //                var edge = bufferData[i].m_Edge;
            //                var edge2 = m_EdgeData[edge];
            //                var curve = m_CurveData[edge];
            //                float2 float5;
            //                if (edge2.m_Start == netEntity) {
            //                    var float4 = curve.m_Bezier.a;
            //                    float5 = math.normalizesafe(
            //                        MathUtils.StartTangent(curve.m_Bezier).xz, default);
            //                    goto IL_0220;
            //                }

            // if (edge2.m_End == netEntity) {
            //                    var float4 = curve.m_Bezier.d;
            //                    float5 = math.normalizesafe(
            //                        -MathUtils.EndTangent(curve.m_Bezier).xz, default);
            //                    goto IL_0220;
            //                }

            // IL_03AB:
            //                i++;
            //                continue;
            //            IL_0220:
            //                flag = false;
            //                var prefabRef = m_PrefabRefData[edge];
            //                var netData = m_PrefabNetData[prefabRef.m_Prefab];
            //                if ((m_NetData.m_RequiredLayers & netData.m_RequiredLayers) ==
            //                    Layer.None) {
            //                    goto IL_03AB;
            //                }

            // var defaultWidth = m_NetGeometryData.m_DefaultWidth;
            //                if ((m_NetGeometryData.m_Flags & Net.GeometryFlags.StrictNodes) ==
            //                    0 &&
            //                    m_PrefabNetGeometryData.TryGetComponent(
            //                        prefabRef.m_Prefab,
            //                        out NetGeometryData componentData3)) {
            //                    defaultWidth = componentData3.m_DefaultWidth;
            //                }

            // int num3;
            //                float num4;
            //                float num5;
            //                if (flag2) {
            //                    var cellWidth =
            //                        ZoneUtils.GetCellWidth(m_NetGeometryData.m_DefaultWidth);
            //                    var cellWidth2 = ZoneUtils.GetCellWidth(defaultWidth);
            //                    num3 = 1 + math.abs(cellWidth2 - cellWidth);
            //                    num4 = (num3 - 1) * -4f;
            //                    num5 = 8f;
            //                } else {
            //                    var num6 =
            //                        math.abs(defaultWidth - m_NetGeometryData.m_DefaultWidth);
            //                    if (num6 > 1.6f) {
            //                        num3 = 3;
            //                        num4 = num6 * -0.5f;
            //                        num5 = num6 * 0.5f;
            //                    } else {
            //                        num3 = 1;
            //                        num4 = 0f;
            //                        num5 = 0f;
            //                    }
            //                }

            // for (var j = 0; j < num3; j++) {
            //                    float3 float4;
            //                    var float6 = float4;
            //                    if (math.abs(num4) >= 0.08f) {
            //                        float6.xz += MathUtils.Left(float5) * num4;
            //                    }

            // var num7 = math.distancesq(
            //                        float6 - float3,
            //                        m_ControlPoint.m_HitPosition);
            //                    if (num7 < num) {
            //                        num = num7;
            //                        @float = float6;
            //                    }

            // num4 += num5;
            //                }

            // var num8 = math.dot(xz, float5);
            //                if (num8 < num2) {
            //                    num2 = num8;
            //                    float2 = float5;
            //                    goto IL_03AB;
            //                }

            // goto IL_03AB;
            //            }
            //        }

            // if (flag) {
            //            var prefabRef2 = m_PrefabRefData[netEntity];
            //            var netData2 = m_PrefabNetData[prefabRef2.m_Prefab];
            //            if ((m_NetData.m_RequiredLayers & netData2.m_RequiredLayers) !=
            //                Layer.None &&
            //                math.distancesq(
            //                    componentData.m_Position - float3,
            //                    m_ControlPoint.m_HitPosition) <
            //                num) {
            //                @float = componentData.m_Position;
            //            }
            //        }

            // if (num != 3.4028235E+38f) {
            //            controlPoint.m_Position = @float - float3;
            //            controlPoint.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f,
            //                m_ControlPoint.m_HitPosition.xz * m_SnapFactor,
            //                controlPoint.m_Position.xz * m_SnapFactor, controlPoint.m_Direction);
            //            AddSnapPosition(ref m_BestSnapPosition, controlPoint);
            //            if (num2 != 3.4028235E+38f && !m_LocalTangent.Equals(default)) {
            //                controlPoint.m_Rotation = quaternion.RotateY(
            //                    MathUtils.RotationAngleSignedRight(m_LocalTangent, -float2));
            //                controlPoint.m_Direction =
            //                    math.normalizesafe(
            //                        math.forward(controlPoint.m_Rotation).xz,
            //                        default);
            //                controlPoint.m_Position =
            //                    @float - math.mul(controlPoint.m_Rotation, m_LocalOffset);
            //                controlPoint.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f,
            //                    m_ControlPoint.m_HitPosition.xz * m_SnapFactor,
            //                    controlPoint.m_Position.xz * m_SnapFactor,
            //                    controlPoint.m_Direction);
            //                AddSnapPosition(ref m_BestSnapPosition, controlPoint);
            //            }

            // var value = default(SubSnapPoint);
            //            value.m_Position = @float;
            //            value.m_Tangent = float2;
            //            m_SubSnapPoints.Add(in value);
            //        }
            //    }

            // public ControlPoint m_ControlPoint;

            // public ControlPoint m_BestSnapPosition;

            // public quaternion m_Rotation;

            // public Bounds2 m_Bounds;

            // public float3 m_LocalOffset;

            // public float2 m_LocalTangent;

            // public Entity m_IgnoreOwner;

            // public float m_SnapFactor;

            // public NetData m_NetData;

            // public NetGeometryData m_NetGeometryData;

            // public RoadData m_RoadData;

            // public NativeList<SubSnapPoint> m_SubSnapPoints;

            // public ComponentLookup<Owner> m_OwnerData;

            // public ComponentLookup<Net.Node> m_NodeData;

            // public ComponentLookup<Edge> m_EdgeData;

            // public ComponentLookup<Curve> m_CurveData;

            // public ComponentLookup<PrefabRef> m_PrefabRefData;

            // public ComponentLookup<NetData> m_PrefabNetData;

            // public ComponentLookup<NetGeometryData> m_PrefabNetGeometryData;

            // public BufferLookup<ConnectedEdge> m_ConnectedEdges;
            // }

            // private struct ZoneBlockIterator : INativeQuadTreeIterator<Entity, Bounds2>,
            //    IUnsafeQuadTreeIterator<Entity, Bounds2> {
            //    public bool Intersect(Bounds2 bounds) {
            //        return MathUtils.Intersect(bounds, m_Bounds);
            //    }

            // public void Iterate(Bounds2 bounds, Entity blockEntity) {
            //        if (!MathUtils.Intersect(bounds, m_Bounds)) {
            //            return;
            //        }

            // if (m_IgnoreOwner != Entity.Null) {
            //            var entity = blockEntity;
            //            while (m_OwnerData.TryGetComponent(entity, out Owner componentData)) {
            //                if (componentData.m_Owner == m_IgnoreOwner) {
            //                    return;
            //                }

            // entity = componentData.m_Owner;
            //            }
            //        }

            // var block = m_BlockData[blockEntity];
            //        var quad = ZoneUtils.CalculateCorners(block);
            //        var segment = new Line2.Segment(quad.a, quad.b);
            //        var line2 = new Line2.Segment(
            //            m_ControlPoint.m_HitPosition.xz,
            //            m_ControlPoint.m_HitPosition.xz);
            //        var @float = m_Direction *
            //                     (math.max(0f, m_LotSize.y - m_LotSize.x) * 4f);
            //        line2.a -= @float;
            //        line2.b += @float;
            //        var num = MathUtils.Distance(segment, line2, out float2 t);
            //        if (num == 0f) {
            //            num -= 0.5f - math.abs(t.y - 0.5f);
            //        }

            // if (num >= m_BestDistance) {
            //            return;
            //        }

            // m_BestDistance = num;
            //        var y = m_ControlPoint.m_HitPosition.xz - block.m_Position.xz;
            //        var float2 = MathUtils.Left(block.m_Direction);
            //        var num2 = block.m_Size.y * 4f;
            //        var num3 = m_LotSize.y * 4f;
            //        var num4 = math.dot(block.m_Direction, y);
            //        var num5 = math.dot(float2, y);
            //        var num6 = math.select(0f, 0.5f, ((block.m_Size.x ^ m_LotSize.x) & 1) != 0);
            //        num5 -= (math.round((num5 / 8f) - num6) + num6) * 8f;
            //        m_BestSnapPosition = m_ControlPoint;
            //        m_BestSnapPosition.m_Position = m_ControlPoint.m_HitPosition;
            //        m_BestSnapPosition.m_Position.xz = m_BestSnapPosition.m_Position.xz +
            //                                           (block.m_Direction * (num2 - num3 - num4));
            //        m_BestSnapPosition.m_Position.xz =
            //            m_BestSnapPosition.m_Position.xz - (float2 * num5);
            //        m_BestSnapPosition.m_Direction = block.m_Direction;
            //        m_BestSnapPosition.m_Rotation =
            //            ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);
            //        m_BestSnapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f,
            //            m_ControlPoint.m_HitPosition.xz * 0.5f,
            //            m_BestSnapPosition.m_Position.xz * 0.5f, m_BestSnapPosition.m_Direction);
            //        m_BestSnapPosition.m_OriginalEntity = blockEntity;
            //    }

            // public ControlPoint m_ControlPoint;

            // public ControlPoint m_BestSnapPosition;

            // public float m_BestDistance;

            // public int2 m_LotSize;

            // public Bounds2 m_Bounds;

            // public float2 m_Direction;

            // public Entity m_IgnoreOwner;

            // public ComponentLookup<Owner> m_OwnerData;

            // public ComponentLookup<Block> m_BlockData;
            // }
        }
    }
}
