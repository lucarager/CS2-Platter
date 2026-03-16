// <copyright file="P_OverlaySystem.PrepareOverlayCommandsJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Components;
    using Constants;
    using Game.Common;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;
    using FrustumPlanes = Game.Rendering.FrustumPlanes;
    using Transform = Game.Objects.Transform;

    #endregion

    public partial class P_OverlaySystem {
        /// <summary>
        /// Parallel job that iterates parcel chunks, computes all geometry, and emits
        /// <see cref="OverlayDrawCommand"/>s into a <see cref="NativeStream"/>.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        internal struct PrepareOverlayCommandsJob : IJobChunk {
            [ReadOnly] public required NativeHashMap<ushort, Color>     m_ColorsMap;
            [ReadOnly] public required EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Transform>   m_TransformComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Temp>        m_TempComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>   m_PrefabRefComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Parcel>      m_ParcelComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Transform>       m_TransformComponentLookup;
            [ReadOnly] public required ComponentLookup<Parcel>          m_ParcelComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelData>      m_ParcelDataComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelSpawnable> m_ParcelSpawnableComponentLookup;
            [ReadOnly] public required ZoneType                         m_CurrentPreZoneType;
            [ReadOnly] public required NativeArray<FrustumPlanes.PlanePacket4> m_CullingPlanes;

            public NativeStream.Writer m_CommandWriter;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                m_CommandWriter.BeginForEachIndex(unfilteredChunkIndex);

                var enumerator      = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var entitiesArray   = chunk.GetNativeArray(m_EntityTypeHandle);
                var transformsArray = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);
                var prefabRefsArray = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var parcelsArray    = chunk.GetNativeArray(ref m_ParcelComponentTypeHandle);
                var tempArray       = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var parcelSourceEntity = entitiesArray[i];
                    var prefabRef          = prefabRefsArray[i];
                    var isTemp             = chunk.Has(ref m_TempComponentTypeHandle);
                    var isHoverPreview     = isTemp && (tempArray[i].m_Flags & (TempFlags.Select | TempFlags.Delete)) != 0;
                    var isPlacementTemp    = isTemp && (tempArray[i].m_Flags & (TempFlags.Select | TempFlags.Delete)) == 0;

                    if (isHoverPreview) {
                        parcelSourceEntity = tempArray[i].m_Original;
                    }

                    var transform   = isHoverPreview ? m_TransformComponentLookup[parcelSourceEntity] : transformsArray[i];
                    var parcel      = isHoverPreview ? m_ParcelComponentLookup[parcelSourceEntity] : parcelsArray[i];
                    var zoneIndex   = isPlacementTemp ? m_CurrentPreZoneType : parcel.m_PreZoneType;
                    var isSpawnable = m_ParcelSpawnableComponentLookup.HasComponent(parcelSourceEntity);

                    if (!m_ParcelDataComponentLookup.TryGetComponent(prefabRef, out var parcelData)) {
                        continue;
                    }

                    // Frustum cull: compute world-space AABB from rotated parcel extents
                    var halfSize     = ParcelGeometryUtils.GetParcelSize(parcelData.m_LotSize) * 0.5f;
                    var rotMatrix    = new float3x3(transform.m_Rotation);
                    var worldExtents = math.abs(rotMatrix.c0) * halfSize.x
                                     + math.abs(rotMatrix.c1) * halfSize.y
                                     + math.abs(rotMatrix.c2) * halfSize.z;

                    if (!IsVisibleInFrustum(m_CullingPlanes, transform.m_Position, worldExtents)) {
                        continue;
                    }

                    EmitParcelCommands(
                        ref m_CommandWriter,
                        parcelData.m_LotSize,
                        ParcelGeometryUtils.GetTransformMatrix(transform),
                        m_ColorsMap[zoneIndex.m_Index],
                        parcel.m_State,
                        isSpawnable || isTemp,
                        isHoverPreview
                    );
                }

                m_CommandWriter.EndForEachIndex();
            }

            /// <summary>
            /// Vectorized AABB-vs-frustum test matching <see cref="FrustumPlanes.Intersect"/> logic
            /// without requiring unsafe pointers (uses NativeArray indexer instead).
            /// </summary>
            private static bool IsVisibleInFrustum(NativeArray<FrustumPlanes.PlanePacket4> planePackets,
                                                   float3 center, float3 extents) {
                var cx       = center.xxxx;
                var cy       = center.yyyy;
                var cz       = center.zzzz;
                var ex       = extents.xxxx;
                var ey       = extents.yyyy;
                var ez       = extents.zzzz;
                var outCount = (int4)0;
                for (var i = 0; i < planePackets.Length; i++) {
                    var p      = planePackets[i];
                    var dist   = p.Xs * cx + p.Ys * cy + p.Zs * cz + p.Distances;
                    var radius = math.abs(p.Xs) * ex + math.abs(p.Ys) * ey + math.abs(p.Zs) * ez;
                    outCount  += (int4)(dist + radius < 0f);
                }
                return math.csum(outCount) == 0;
            }

            /// <summary>
            /// Create all commands needed to render a parcel
            /// </summary>
            private static void EmitParcelCommands(ref NativeStream.Writer writer,
                                                   int2                    lotSize,
                                                   float4x4               trs,
                                                   Color                  backgroundColor,
                                                   ParcelState            parcelState,
                                                   bool                   spawnable,
                                                   bool                   highlighted) {
                // Constants
                const float accessMult         = 3.5f;
                const float opacityLow         = 0.15f;
                const float opacityMedium      = 0.4f;
                const float opacityHigh        = 1f;
                const float outlineWidth       = DimensionConstants.ParcelOutlineWidth;
                const float cellSize           = DimensionConstants.CellSize;
                const float cellOutlineWidth   = DimensionConstants.ParcelCellOutlineWidth;
                const float frontIndicatorDiam = DimensionConstants.ParcelFrontIndicatorDiameter;
                const float frontIndicatorLine = DimensionConstants.ParcelFrontIndicatorHollowLineWidth;

                // Colors
                var parcelOutlineColor        = new Color(1f, 1f, 1f, highlighted ? opacityHigh : opacityMedium);
                var parcelInlineColor         = new Color(1f, 1f, 1f, opacityLow);
                var parcelFrontIndicatorColor = new Color(1f, 1f, 1f, 1f);
                var transparentColor          = new Color(1f, 1f, 1f, 0f);

                // Geometry
                var parcelGeo     = new ParcelGeometry(lotSize);
                var parcelSize    = parcelGeo.Size;
                var parcelCenter  = parcelGeo.Center;
                var parcelCorners = parcelGeo.CornerNodes;
                var parcelFront   = parcelGeo.FrontNode;
                var parcelBack    = parcelGeo.BackNode;

                // Road access flags
                var hasFrontAccess = (parcelState & ParcelState.RoadFront) != 0;
                var hasLeftAccess  = (parcelState & ParcelState.RoadLeft)  != 0;
                var hasRightAccess = (parcelState & ParcelState.RoadRight) != 0;

                // Line widths (thicker on sides with road access)
                var frontLineWidth = hasFrontAccess ? outlineWidth * accessMult : outlineWidth;
                var leftLineWidth  = hasLeftAccess  ? outlineWidth * accessMult : outlineWidth;
                var rightLineWidth = hasRightAccess ? outlineWidth * accessMult : outlineWidth;
                var frontLineHalf  = frontLineWidth * 0.5f;
                var leftLineHalf   = leftLineWidth  * 0.5f;
                var backLineHalf   = outlineWidth   * 0.5f;
                var rightLineHalf  = rightLineWidth * 0.5f;

                // --- Outline edges ---
                // Corner layout:
                //  c2 ┌┐ c3
                //  c1 └┘ c0

                // Front edge
                WriteLine(ref writer, parcelOutlineColor, parcelOutlineColor, 0f, 0, frontLineWidth, 0f,
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c0 + new float3(+rightLineWidth, 0f, -frontLineHalf)),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c1 + new float3(-leftLineWidth,  0f, -frontLineHalf)));

                // Left edge
                WriteLine(ref writer, parcelOutlineColor, parcelOutlineColor, 0f, 0, leftLineWidth, 0f,
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c1 + new float3(-leftLineHalf, 0f, 0f)),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c2 + new float3(-leftLineHalf, 0f, 0f)));

                // Back edge
                WriteLine(ref writer, parcelOutlineColor, parcelOutlineColor, 0f, 0, outlineWidth, 0f,
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c2 + new float3(-leftLineWidth,  0f, +backLineHalf)),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c3 + new float3(+rightLineWidth, 0f, +backLineHalf)));

                // Right edge
                WriteLine(ref writer, parcelOutlineColor, parcelOutlineColor, 0f, 0, rightLineWidth, 0f,
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c3 + new float3(+rightLineHalf, 0f, 0f)),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c0 + new float3(+rightLineHalf, 0f, 0f)));

                // --- Background fill ---
                backgroundColor.a = opacityLow;
                WriteLine(ref writer, backgroundColor, backgroundColor, 0f, 0, parcelSize.x, 0f,
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelFront),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelBack));

                // --- Inner grid lines (front to back) ---
                var frontNode = parcelCorners.c1;
                var backNode  = parcelCorners.c2;
                frontNode.z -= frontLineWidth;
                backNode.z  += outlineWidth;
                for (var i = 1; i < lotSize.x; i++) {
                    frontNode.x -= cellSize;
                    backNode.x  -= cellSize;
                    WriteLine(ref writer, parcelInlineColor, parcelInlineColor, 0f, 0, cellOutlineWidth, 0f,
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, frontNode),
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, backNode));
                }

                // --- Inner grid lines (left to right) ---
                var leftNode  = parcelCorners.c1;
                var rightNode = parcelCorners.c0;
                leftNode.x  -= leftLineWidth;
                rightNode.x += rightLineWidth;
                for (var i = 1; i < lotSize.y; i++) {
                    leftNode.z  -= cellSize;
                    rightNode.z -= cellSize;
                    WriteLine(ref writer, parcelInlineColor, parcelInlineColor, 0f, 0, cellOutlineWidth, 0f,
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, leftNode),
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, rightNode));
                }

                // --- Front access indicator ---
                WriteCircle(ref writer,
                    parcelFrontIndicatorColor,
                    spawnable ? parcelFrontIndicatorColor : transparentColor,
                    frontIndicatorLine, 0, frontIndicatorDiam, new float2(1, 1),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelFront));
            }

            private static void WriteLine(ref NativeStream.Writer writer,
                                          Color outlineColor, Color fillColor,
                                          float outlineWidth, int styleFlags,
                                          float width, float roundness,
                                          float3 a, float3 b) {
                writer.Write(new OverlayDrawCommand {
                    m_Type         = OverlayCommandType.Line,
                    m_OutlineColor = outlineColor,
                    m_FillColor    = fillColor,
                    m_OutlineWidth = outlineWidth,
                    m_StyleFlags   = styleFlags,
                    m_Width        = width,
                    m_Extra        = roundness,
                    m_PointA       = a,
                    m_PointB       = b,
                });
            }

            private static void WriteCircle(ref NativeStream.Writer writer,
                                            Color outlineColor, Color fillColor,
                                            float outlineWidth, int styleFlags,
                                            float diameter, float2 direction,
                                            float3 position) {
                writer.Write(new OverlayDrawCommand {
                    m_Type         = OverlayCommandType.Circle,
                    m_OutlineColor = outlineColor,
                    m_FillColor    = fillColor,
                    m_OutlineWidth = outlineWidth,
                    m_StyleFlags   = styleFlags,
                    m_Width        = diameter,
                    m_Extra        = direction,
                    m_PointA       = position,
                });
            }
        }
    }
}
