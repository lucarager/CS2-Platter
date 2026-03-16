// <copyright file="P_OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using Colossal.Mathematics;
    using Components;
    using Constants;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Game.Zones;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;
    using static Game.Rendering.OverlayRenderSystem;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Overlay Rendering System.
    /// <todo>Check BuildingLotRenderJob for any optimizations to grab</todo>
    /// </summary>
    public partial class P_OverlaySystem : PlatterGameSystemBase {
        private EntityQuery         m_ParcelQuery;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private P_ZoneCacheSystem   m_ZoneCacheSystem;
        private ToolSystem          m_ToolSystem;
        private P_UISystem          m_PlatterUISystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Queries
            m_ParcelQuery = SystemAPI.QueryBuilder()
                                     .WithAll<Parcel>()
                                     .WithNone<Hidden, Deleted>()
                                     .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_ZoneCacheSystem     = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PlatterUISystem     = World.GetOrCreateSystemManaged<P_UISystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!ShouldRenderOverlay()) {
                return;
            }

            try {
                if (Camera.main is null) {
                    throw new NullReferenceException("Camera.main is null");
                }

                var edgeColors = m_ZoneCacheSystem.FillColors;
                var colorsMap  = new NativeHashMap<ushort, Color>(edgeColors.Count, Allocator.Temp);

                foreach (var entry in edgeColors) {
                    colorsMap.Add(entry.Key, entry.Value);
                }

                // Todo split position calc, culling/render calc, and render step, into separate jobs for perf
                var drawOverlaysJobData = new DrawOverlaysJob
                {
                    m_OverlayRenderBuffer            = m_OverlayRenderSystem.GetBuffer(out var overlayRenderBufferJobHandle),
                    m_ColorsMap                      = colorsMap,
                    m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                    m_TransformComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<Transform>(),
                    m_PrefabRefComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    m_ParcelComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<Parcel>(),
                    m_TempComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Temp>(),
                    m_TransformComponentLookup       = SystemAPI.GetComponentLookup<Transform>(),
                    m_ParcelComponentLookup          = SystemAPI.GetComponentLookup<Parcel>(),
                    m_ParcelDataComponentLookup      = SystemAPI.GetComponentLookup<ParcelData>(),
                    m_ParcelSpawnableComponentLookup = SystemAPI.GetComponentLookup<ParcelSpawnable>(),
                    m_CurrentPreZoneType             = m_PlatterUISystem.PreZoneType,
                };

                var drawOverlaysJob = drawOverlaysJobData.ScheduleByRef(
                    m_ParcelQuery,
                    JobHandle.CombineDependencies(
                        Dependency,
                        overlayRenderBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawOverlaysJob);

                drawOverlaysJob.Complete();
                colorsMap.Dispose();

                Dependency = drawOverlaysJob;
            } catch (Exception ex) {
                m_Log.Error($"Failed on DrawOverlaysJob:\n{ex}");
            }
        }

        /// <summary>
        /// Determines if parcel overlay should be rendered based on active tool and settings.
        /// </summary>
        private bool ShouldRenderOverlay() {
            // Always show if explicitly enabled via RenderParcels setting.
            if (PlatterMod.Instance.Settings.RenderParcels) {
                return true;
            }

            // Always show when using parcel prefabs.
            if (m_ToolSystem.activePrefab is ParcelPlaceholderPrefab) {
                return true;
            }

            // Always show when bulldozing.
            if (m_ToolSystem.activeTool is BulldozeToolSystem) {
                return true;
            }

            // If vanilla tool overlay is disabled, only show for parcel tools
            if (!PlatterMod.Instance.Settings.EnableOverlayForTools) {
                return false;
            }

            // Show parcels for compatible vanilla tools (when EnableOverlayForVanillaTools is true)
            var toolCheck = m_ToolSystem.activeTool is not DefaultToolSystem &&
                            m_ToolSystem.activeTool is not AreaToolSystem    &&
                            m_ToolSystem.activeTool is not WaterToolSystem   &&
                            m_ToolSystem.activeTool is not UpgradeToolSystem &&
                            // Object tool only when using parcel prefabs
                            m_ToolSystem.activeTool is not ObjectToolSystem {
                                prefab: not ParcelPlaceholderPrefab,
                            };

            return toolCheck;
        }

        /// <summary>
        /// Job to draw parcel overlays.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer       m_OverlayRenderBuffer;
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

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                var enumerator       = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var entitiesArray    = chunk.GetNativeArray(m_EntityTypeHandle);
                var transformsArray  = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);
                var prefabRefsArray  = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var parcelsArray     = chunk.GetNativeArray(ref m_ParcelComponentTypeHandle);
                var tempArray        = chunk.GetNativeArray(ref m_TempComponentTypeHandle);
                //var cullingInfoArray = chunk.GetNativeArray(ref m_CullingInfoComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var parcelSourceEntity = entitiesArray[i];
                    var prefabRef          = prefabRefsArray[i];
                    var isTemp             = chunk.Has(ref m_TempComponentTypeHandle);
                    var isHoverPreview     = isTemp && (tempArray[i].m_Flags & TempFlags.Select) != 0;
                    var isTempPreview      = isTemp && (tempArray[i].m_Flags & TempFlags.Select) == 0;

                    if (isHoverPreview) {
                        var temp = tempArray[i];
                        parcelSourceEntity = temp.m_Original;
                    }

                    var transform     = isHoverPreview ? m_TransformComponentLookup[parcelSourceEntity] : transformsArray[i];
                    var parcel        = isHoverPreview ? m_ParcelComponentLookup[parcelSourceEntity] : parcelsArray[i];
                    var zoneIndex     = isTempPreview ? m_CurrentPreZoneType : parcel.m_PreZoneType;
                    var isSpawnable   = m_ParcelSpawnableComponentLookup.HasComponent(parcelSourceEntity);
                    var isHighlighted = isHoverPreview;
                    var trs           = ParcelGeometryUtils.GetTransformMatrix(transform);


                    if (!m_ParcelDataComponentLookup.TryGetComponent(prefabRef, out var parcelData)) {
                        continue;
                    }

                    DrawParcel(
                        m_OverlayRenderBuffer,
                        entitiesArray[i],
                        parcelData.m_LotSize,
                        trs,
                        m_ColorsMap[zoneIndex.m_Index],
                        parcel.m_State,
                        isSpawnable || isTemp,
                        isHighlighted
                    );
                }
            }

            private static void DrawParcel(OverlayRenderSystem.Buffer buffer,
                                           Entity entity,
                                          int2                       lotSize,
                                          float4x4                   trs,
                                          Color                      backgroundColor,
                                          ParcelState                parcelState,
                                          bool                       spawnable = false,
                                          bool highlighted = false) {
                // Constants
                const float accessMult         = 3.5f;
                const float opacityLow         = 0.13f;
                const float opacityMedium      = 0.4f;
                const float opacityHigh        = 1f;
                const float outlineWidth       = DimensionConstants.ParcelOutlineWidth;
                const float cellSize           = DimensionConstants.CellSize;
                const float cellOutlineWidth   = DimensionConstants.ParcelCellOutlineWidth;
                const float frontIndicatorDiam = DimensionConstants.ParcelFrontIndicatorDiameter;
                const float frontIndicatorLine = DimensionConstants.ParcelFrontIndicatorHollowLineWidth;

                // Colors
                var parcelOutlineColor        = new Color(1f, 1f, 1f, highlighted ? opacityHigh : opacityMedium);
                var parcelInlineColor         = new Color(1f, 1f, 1f, highlighted ? opacityMedium : opacityLow);
                var parcelFrontIndicatorColor = new Color(1f, 1f, 1f, 1f);
                var transparentColor          = new Color(1f, 1f, 1f, 0f);

                // Geometry calculations
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

                // Draw parcel outline
                // Corner layout:
                //  c2 ┌┐ c3
                //  c1 └┘ c0

                // Front edge
                var frontSegment = new Line3.Segment(
                                                     ParcelGeometryUtils.GetWorldPosition(
                                                                                          trs,
                                                                                          parcelCenter,
                                                                                          parcelCorners.c0 + new float3(+rightLineWidth, 0f, -frontLineHalf)),
                                                     ParcelGeometryUtils.GetWorldPosition(
                                                                                          trs,
                                                                                          parcelCenter,
                                                                                          parcelCorners.c1 + new float3(-leftLineWidth, 0f, -frontLineHalf))
                                                    );
                //PlatterMod.Instance.Log.Debug($"[{entity}] Drawing FRONT edge from position a {frontSegment.a} to {frontSegment.b}");
                buffer.DrawLine(
                    parcelOutlineColor,
                    parcelOutlineColor,
                    0f,
                    0,
                    frontSegment,
                    frontLineWidth,
                    0f);

                // Left edge
                var leftSegment = new Line3.Segment(
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c1 + new float3(-leftLineHalf, 0f, 0f)),
                        ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c2 + new float3(-leftLineHalf, 0f, 0f))
                    );
                //PlatterMod.Instance.Log.Debug($"[{entity}] Drawing LEFT edge from position a {leftSegment.a} to {leftSegment.b}");
                buffer.DrawLine(
                    parcelOutlineColor,
                    parcelOutlineColor,
                    0f,
                    0,
                    leftSegment,
                    leftLineWidth,
                    0f);

                // Back edge
                var backSegment = new Line3.Segment(
                                                    ParcelGeometryUtils.GetWorldPosition(
                                                                                         trs,
                                                                                         parcelCenter,
                                                                                         parcelCorners.c2 + new float3(-leftLineWidth, 0f, +backLineHalf)),
                                                    ParcelGeometryUtils.GetWorldPosition(
                                                                                         trs,
                                                                                         parcelCenter,
                                                                                         parcelCorners.c3 + new float3(+rightLineWidth, 0f, +backLineHalf))
                                                   );
                //PlatterMod.Instance.Log.Debug($"[{entity}] Drawing BACK edge from position a {backSegment.a} to {backSegment.b}");
                buffer.DrawLine(
                    parcelOutlineColor,
                    parcelOutlineColor,
                    0f,
                    0,
                    backSegment,
                    outlineWidth,
                    0f);

                // Right edge
                var rightSegment = new Line3.Segment(
                                                     ParcelGeometryUtils.GetWorldPosition(
                                                                                          trs,
                                                                                          parcelCenter,
                                                                                          parcelCorners.c3 + new float3(+rightLineHalf, 0f, 0f)),
                                                     ParcelGeometryUtils.GetWorldPosition(
                                                                                          trs,
                                                                                          parcelCenter,
                                                                                          parcelCorners.c0 + new float3(+rightLineHalf, 0f, 0f))
                                                    );
                //PlatterMod.Instance.Log.Debug($"[{entity}] Drawing RIGHT edge from position a {rightSegment.a} to {rightSegment.b}");
                buffer.DrawLine(
                    parcelOutlineColor,
                    parcelOutlineColor,
                    0f,
                    0,
                    rightSegment,
                    rightLineWidth, 
                    0f);

                // Background fill
                var backgroundSegment = new Line3.Segment(
                                                          ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelFront),
                                                          ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelBack)
                                                         );
                backgroundColor.a = opacityLow;
                //PlatterMod.Instance.Log.Debug($"[{entity}] Drawing BACKGROUND edge from position a {backgroundSegment.a} to {backgroundSegment.b}");
                buffer.DrawLine(
                    backgroundColor,
                    backgroundColor,
                    0f,
                    0,
                    backgroundSegment,
                    parcelSize.x, 
                    0f);

                // Inner grid lines (front to back)
                var frontNode = parcelCorners.c1;
                var backNode  = parcelCorners.c2;
                frontNode.z -= frontLineWidth;
                backNode.z  += outlineWidth;
                for (var i = 1; i < lotSize.x; i++) {
                    frontNode.x -= cellSize;
                    backNode.x  -= cellSize;
                    buffer.DrawLine(
                        parcelInlineColor,
                        parcelInlineColor,
                        0f,
                        0,
                        new Line3.Segment(
                            ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, frontNode),
                            ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, backNode)
                        ),
                        cellOutlineWidth,
                        0f);
                }

                // Inner grid lines (left to right)
                var leftNode  = parcelCorners.c1;
                var rightNode = parcelCorners.c0;
                leftNode.x  -= leftLineWidth;
                rightNode.x += rightLineWidth;
                for (var i = 1; i < lotSize.y; i++) {
                    leftNode.z  -= cellSize;
                    rightNode.z -= cellSize;
                    buffer.DrawLine(
                        parcelInlineColor,
                        parcelInlineColor,
                        0f,
                        0,
                        new Line3.Segment(
                            ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, leftNode),
                            ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, rightNode)
                        ),
                        cellOutlineWidth,
                        0f);
                }

                // Front access indicator (filled circle if spawnable, hollow otherwise)
                buffer.DrawCircle(
                    parcelFrontIndicatorColor,
                    spawnable ? parcelFrontIndicatorColor : transparentColor,
                    frontIndicatorLine,
                    0,
                    new float2(1, 1),
                    ParcelGeometryUtils.GetWorldPosition(trs, parcelCenter, parcelFront),
                    frontIndicatorDiam
                );
            }
        }
    }
}