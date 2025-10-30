// <copyright file="P_BuildingPrefabClassifySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Unity.Burst;

namespace Platter.Systems {
    using System.Collections.Generic;
    using Colossal.Mathematics;
    using Game.Prefabs;
    using Unity.Burst.Intrinsics;
    using Unity.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    /// <summary>
    /// System responsible for adding the GrowableBuilding and LinkedParcel components to buildings.
    /// </summary>
    public partial class P_BuildingPrefabClassifySystem : GameSystemBase {
        private EntityQuery          m_BuildingPrefabQuery;
        private PrefixedLogger       m_Log;
        private ModificationBarrier2 m_ModificationBarrier2;

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
                                             .WithNone<BoundarySubObjectData, BoundarySubAreaNodeData, BoundarySubLaneData,
                                                 BoundarySubNetData>()
                                             .Build();

            m_ModificationBarrier2 = World.GetOrCreateSystemManaged<ModificationBarrier2>();

            // Update Cycle
            RequireForUpdate(m_BuildingPrefabQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var jobHandle = new ProcessBuildingPrefabJob(
                SystemAPI.GetEntityTypeHandle(),
                SystemAPI.GetComponentTypeHandle<ObjectGeometryData>(),
                SystemAPI.GetBufferTypeHandle<SubAreaNode>(),
                SystemAPI.GetBufferTypeHandle<SubObject>(),
                SystemAPI.GetBufferTypeHandle<SubLane>(),
                SystemAPI.GetBufferTypeHandle<SubNet>(),
                SystemAPI.GetBufferTypeHandle<SubArea>(),
                m_ModificationBarrier2.CreateCommandBuffer().AsParallelWriter()
            ).ScheduleParallel(m_BuildingPrefabQuery, Dependency);

            m_ModificationBarrier2.AddJobHandleForProducer(jobHandle);

            Dependency = jobHandle;
        }

        public struct ProcessBuildingPrefabJob : IJobChunk {
            [ReadOnly] private EntityTypeHandle                        m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<ObjectGeometryData> m_ObjectGeometryDataTypeHandle;
            [ReadOnly] private BufferTypeHandle<SubAreaNode>           m_SubAreaNodeTypeHandle;
            [ReadOnly] private BufferTypeHandle<SubObject>             m_SubObjectTypeHandle;
            [ReadOnly] private BufferTypeHandle<SubLane>               m_SubLaneTypeHandle;
            [ReadOnly] private BufferTypeHandle<SubNet>                m_SubNetTypeHandle;
            [ReadOnly] private BufferTypeHandle<SubArea>               m_SubAreaTypeHandle;
            private            EntityCommandBuffer.ParallelWriter      m_CommandBuffer;
            private const      float                                   ShiftBase = 2f;

            public ProcessBuildingPrefabJob(EntityTypeHandle entityTypeHandle,
                                      ComponentTypeHandle<ObjectGeometryData> objectGeometryDataTypeHandle,
                                      BufferTypeHandle<SubAreaNode> subAreaNodeTypeHandle,
                                      BufferTypeHandle<SubObject> subObjectTypeHandle,
                                      BufferTypeHandle<SubLane> subLaneTypeHandle, 
                                      BufferTypeHandle<SubNet> subNetTypeHandle,
                                      BufferTypeHandle<SubArea> subAreaTypeHandle,
                                      EntityCommandBuffer.ParallelWriter commandBuffer) {
                m_EntityTypeHandle             = entityTypeHandle;
                m_ObjectGeometryDataTypeHandle = objectGeometryDataTypeHandle;
                m_SubAreaNodeTypeHandle        = subAreaNodeTypeHandle;
                m_SubObjectTypeHandle          = subObjectTypeHandle;
                m_SubLaneTypeHandle            = subLaneTypeHandle;
                m_SubNetTypeHandle             = subNetTypeHandle;
                m_SubAreaTypeHandle            = subAreaTypeHandle;
                m_CommandBuffer                = commandBuffer;
            }

            public void Execute(in ArchetypeChunk chunk, int index, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray               = chunk.GetNativeArray(m_EntityTypeHandle);
                var objectGeoDataArray        = chunk.GetNativeArray(ref m_ObjectGeometryDataTypeHandle);
                var subAreaNodeBufferAccessor = chunk.GetBufferAccessor(ref m_SubAreaNodeTypeHandle);
                var subObjectBufferAccessor   = chunk.GetBufferAccessor(ref m_SubObjectTypeHandle);
                var subLaneBufferAccessor     = chunk.GetBufferAccessor(ref m_SubLaneTypeHandle);
                var subNetBufferAccessor      = chunk.GetBufferAccessor(ref m_SubNetTypeHandle);
                var subAreaBufferAccessor     = chunk.GetBufferAccessor(ref m_SubAreaTypeHandle);
                var shiftLeft                 = new float3(-ShiftBase, 0f, 0f);
                var shiftRight                = new float3(ShiftBase, 0f, 0f);
                var shiftForward              = new float3(0f, 0f, -ShiftBase);
                var shiftBack                 = new float3(0f, 0f, ShiftBase);
                var shiftDown                 = new float3(0f, -ShiftBase, 0f);
                var shiftUp                   = new float3(0f, ShiftBase, 0f);
                var shiftForLowerBound        = shiftForward + shiftLeft  + shiftDown;
                var shiftForUpperBound        = shiftBack    + shiftRight + shiftUp;

                for (var e = 0; e < chunk.Count; e++) {
                    var prefabEntity      = entityArray[e];
                    var geometry          = objectGeoDataArray[e];
                    var subAreabuffer     = subAreaBufferAccessor[e];
                    var subAreaNodeBuffer = subAreaNodeBufferAccessor[e];
                    var subObjectBuffer   = subObjectBufferAccessor[e];
                    var subLaneBuffer     = subLaneBufferAccessor[e];
                    var subNetBuffer      = subNetBufferAccessor[e];

                    var boundarySubObjectBuffer   = m_CommandBuffer.AddBuffer<BoundarySubObjectData>(index, prefabEntity);
                    var boundarySubAreaNodeBuffer = m_CommandBuffer.AddBuffer<BoundarySubAreaNodeData>(index, prefabEntity);
                    var boundarySubLaneBuffer     = m_CommandBuffer.AddBuffer<BoundarySubLaneData>(index, prefabEntity);
                    var boundarySubNetBuffer      = m_CommandBuffer.AddBuffer<BoundarySubNetData>(index, prefabEntity);

                    var cornerRightFront = new float3(geometry.m_Bounds.max.x, 0, geometry.m_Bounds.min.z);
                    var cornerLeftFront  = new float3(geometry.m_Bounds.min.x, 0, geometry.m_Bounds.min.z);
                    var cornerLeftBack   = new float3(geometry.m_Bounds.min.x, 0, geometry.m_Bounds.max.z);
                    var cornerRightBack  = new float3(geometry.m_Bounds.max.x, 0, geometry.m_Bounds.max.z);

                    //// Front
                    //var bounds = new Dictionary<string, Bounds3> {
                    //{
                    //"front", new Bounds3(
                    //    cornerLeftFront  + shiftForLowerBound,
                    //    cornerRightFront + shiftForUpperBound
                    //)
                    //}, {
                    //"left", new Bounds3(
                    //    cornerLeftFront + shiftForLowerBound,
                    //    cornerLeftBack  + shiftForUpperBound
                    //)
                    //}, {
                    //"rear", new Bounds3(
                    //    cornerLeftBack  + shiftForLowerBound,
                    //    cornerRightBack + shiftForUpperBound
                    //)
                    //}, {
                    //"right", new Bounds3(
                    //    cornerRightFront + shiftForLowerBound,
                    //    cornerRightBack  + shiftForUpperBound
                    //)
                    //},
                    //};

                    //if (chunk.Has(ref m_SubAreaTypeHandle) && chunk.Has(ref m_SubAreaNodeTypeHandle)) {
                    //    for (var i = 0; i < subAreabuffer.Length; i++) {
                    //        var subArea = subAreabuffer[i];

                    //        if (subArea.m_NodeRange.y > subAreaNodeBuffer.Length) {
                    //            continue;
                    //        }

                    //        for (var j = subArea.m_NodeRange.x; j < subArea.m_NodeRange.y; j++) {
                    //            var node    = subAreaNodeBuffer[j];
                    //            var data = new BoundarySubAreaNodeData {
                    //                absIndex  = j,
                    //                relIndex  = j - subArea.m_NodeRange.x,
                    //                areaIndex = i,
                    //            };

                    //            foreach (var (name, boundary) in bounds) {
                    //                if (!MathUtils.Intersect(boundary, node.m_Position)) {
                    //                    continue;
                    //                }

                    //                switch (name) {
                    //                    case "front":
                    //                        data.projectionFilter.x    = true;
                    //                        data.projectionConfig.c0.x = 0f; // distance to front
                    //                        data.projectionConfig.c0.y = 0f; // projection onto front                                            
                    //                        break;
                    //                    case "left":
                    //                        data.projectionFilter.y    = true;
                    //                        data.projectionConfig.c1.x = 0f; // distance to left
                    //                        data.projectionConfig.c1.y = 0f; // projection onto left                                            
                    //                        break;
                    //                    case "rear":
                    //                        data.projectionFilter.z    = true;
                    //                        data.projectionConfig.c2.x = 0f; // distance to rear
                    //                        data.projectionConfig.c2.y = 0f; // projection onto rear                                            
                    //                        break;
                    //                    case "right":
                    //                        data.projectionFilter.w    = true;
                    //                        data.projectionConfig.c3.x = 0f; // distance to right
                    //                        data.projectionConfig.c3.y = 0f; // projection onto right                                            
                    //                        break;
                    //                }
                    //            }

                    //            if (!math.any(data.projectionFilter)) {
                    //                continue;
                    //            }

                    //            boundarySubAreaNodeBuffer.Add(data);
                    //            //node.m_Position      += shift;
                    //            //subAreaNodeBuffer[i] =  node;
                    //        }
                    //    }
                    //}

                    //if (chunk.Has(ref m_SubObjectTypeHandle)) {
                    //    for (var i = 0; i < subObjectBuffer.Length; i++) {
                    //        var subObject = subObjectBuffer[i];
                    //        var shift     = new float3();
                    //        var shifted   = false;
                    //        var data = new BoundarySubObjectData {
                    //        index = i,
                    //        };

                    //        foreach (var (name, boundary) in bounds) {
                    //            if (!MathUtils.Intersect(boundary, subObject.m_Position)) {
                    //                continue;
                    //            }

                    //            shifted = true;

                    //            switch (name) {
                    //                case "front":
                    //                    data.projectionFilter.x = true;
                    //                    data.projectionConfig.c0.x = 0f; // distance to front
                    //                    data.projectionConfig.c0.y = 0f; // projection onto front                                            
                    //                    break;
                    //                case "left":
                    //                    data.projectionFilter.y = true;
                    //                    data.projectionConfig.c1.x = 0f; // distance to left
                    //                    data.projectionConfig.c1.y = 0f; // projection onto left                                            
                    //                    break;
                    //                case "rear":
                    //                    data.projectionFilter.z = true;
                    //                    data.projectionConfig.c2.x = 0f; // distance to rear
                    //                    data.projectionConfig.c2.y = 0f; // projection onto rear                                            
                    //                    break;
                    //                case "right":
                    //                    data.projectionFilter.w = true;
                    //                    data.projectionConfig.c3.x = 0f; // distance to right
                    //                    data.projectionConfig.c3.y = 0f; // projection onto right                                            
                    //                    break;
                    //            }
                    //        }

                    //        if (!shifted) {
                    //            continue;
                    //        }

                    //        boundarySubObjectBuffer.Add(data);
                    //        //subObject.m_Position += shift;
                    //        //subObjectBuffer[i]   =  subObject;
                    //    }
                    //}

                    //if (chunk.Has(ref m_SubLaneTypeHandle)) {
                    //    for (var i = 0; i < subLaneBuffer.Length; i++) {
                    //        var subLane = subLaneBuffer[i];
                    //        var points  = new[] { subLane.m_Curve.a, subLane.m_Curve.b, subLane.m_Curve.c, subLane.m_Curve.d };
                    //        var shifts  = new float3[4];
                    //        var shifted = false;

                    //        var data = new BoundarySubLaneData {
                    //        index = i,
                    //        };

                    //        foreach (var (name, boundary) in bounds) {
                    //            for (var j = 0; j < points.Length; j++) {
                    //                if (!MathUtils.Intersect(boundary, points[j])) {
                    //                    continue;
                    //                }

                    //                shifted = true;

                    //                // get a ref to the correct slot (avoids copying and repeated switch logic later)
                    //                ref var projectionConfig = ref data.projectionConfig0;
                    //                switch (j) {
                    //                    case 1:
                    //                        projectionConfig = ref data.projectionConfig1;
                    //                        break;
                    //                    case 2:
                    //                        projectionConfig = ref data.projectionConfig2;
                    //                        break;
                    //                    case 3:
                    //                        projectionConfig = ref data.projectionConfig3;
                    //                        break;
                    //                }

                    //                //ref var shift = ref shifts[j];
                    //                switch (name) {
                    //                    case "front":
                    //                        projectionConfig.c0.x = 0f; // distance to front
                    //                        projectionConfig.c0.y = 0f; // projection onto front
                    //                        //shift.z                    -= 10;
                    //                        break;
                    //                    case "left":
                    //                        projectionConfig.c1.x = 0f; // distance to left
                    //                        projectionConfig.c1.y = 0f; // projection onto left
                    //                        //shift.x                    -= 10;
                    //                        break;
                    //                    case "rear":
                    //                        projectionConfig.c2.x = 0f; // distance to rear
                    //                        projectionConfig.c2.y = 0f; // projection onto rear
                    //                        //shift.z                    += 10;
                    //                        break;
                    //                    case "right":
                    //                        projectionConfig.c3.x = 0f; // distance to right
                    //                        projectionConfig.c3.y = 0f; // projection onto right
                    //                        //shift.x                    += 10;
                    //                        break;
                    //                }
                    //            }
                    //        }

                    //        if (!shifted) {
                    //            continue;
                    //        }

                    //        boundarySubLaneBuffer.Add(data);

                    //        // Apply all shifts back to the curve
                    //        //subLane.m_Curve.a += shifts[0];
                    //        //subLane.m_Curve.b += shifts[1];
                    //        //subLane.m_Curve.c += shifts[2];
                    //        //subLane.m_Curve.d += shifts[3];

                    //        //subLaneBuffer[i] = subLane;
                    //    }
                    //}

                    //if (chunk.Has(ref m_SubNetTypeHandle)) {
                    //    for (var i = 0; i < subNetBuffer.Length; i++) {
                    //        var subNet  = subNetBuffer[i];
                    //        var points  = new[] { subNet.m_Curve.a, subNet.m_Curve.b, subNet.m_Curve.c, subNet.m_Curve.d };
                    //        var shifts  = new float3[4];
                    //        var shifted = false;

                    //        var data = new BoundarySubNetData {
                    //        index = i,
                    //        };

                    //        foreach (var (name, boundary) in bounds) {
                    //            for (var j = 0; j < points.Length; j++) {
                    //                if (!MathUtils.Intersect(boundary, points[j])) {
                    //                    continue;
                    //                }

                    //                shifted = true;


                    //                // get a ref to the correct slot (avoids copying and repeated switch logic later)
                    //                ref var projectionConfig = ref data.projectionConfig0;
                    //                switch (j) {
                    //                    case 1:
                    //                        projectionConfig = ref data.projectionConfig1;
                    //                        break;
                    //                    case 2:
                    //                        projectionConfig = ref data.projectionConfig2;
                    //                        break;
                    //                    case 3:
                    //                        projectionConfig = ref data.projectionConfig3;
                    //                        break;
                    //                }

                    //                //ref var shift = ref shifts[j];
                    //                switch (name) {
                    //                    case "front":
                    //                        projectionConfig.c0.x = 0f; // distance to front
                    //                        projectionConfig.c0.y = 0f; // projection onto front
                    //                        //shift.z                    -= 10;
                    //                        break;
                    //                    case "left":
                    //                        projectionConfig.c1.x = 0f; // distance to left
                    //                        projectionConfig.c1.y = 0f; // projection onto left
                    //                        //shift.x                    -= 10;
                    //                        break;
                    //                    case "rear":
                    //                        projectionConfig.c2.x = 0f; // distance to rear
                    //                        projectionConfig.c2.y = 0f; // projection onto rear
                    //                        //shift.z                    += 10;
                    //                        break;
                    //                    case "right":
                    //                        projectionConfig.c3.x = 0f; // distance to right
                    //                        projectionConfig.c3.y = 0f; // projection onto right
                    //                        //shift.x                    += 10;
                    //                        break;
                    //                }
                    //            }
                    //        }

                    //        if (!shifted) {
                    //            continue;
                    //        }

                    //        boundarySubNetBuffer.Add(data);

                    //        // Apply all shifts back to the curve
                    //        //subNet.m_Curve.a += shifts[0];
                    //        //subNet.m_Curve.b += shifts[1];
                    //        //subNet.m_Curve.c += shifts[2];
                    //        //subNet.m_Curve.d += shifts[3];

                    //        //subLaneBuffer[i] = subLane;
                    //    }
                    //}
                }
            }
        }
    }
}