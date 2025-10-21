// <copyright file="P_OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Rendering;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine;
    using Transform = Game.Objects.Transform;

    /// <summary>
    /// Overlay Rendering System.
    /// <todo>Check BuildingLotRenderJob for any optimizations to grab</todo>
    /// </summary>
    public partial class P_OverlaySystem : GameSystemBase {
        // Systems & References
        private OverlayRenderSystem m_OverlayRenderSystem;
        private P_ZoneCacheSystem   m_ZoneCacheSystem;
        private PreCullingSystem    m_PreCullingSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelQuery;

        // Data
        private bool m_ShouldRenderParcels = true;
        private bool m_ShouldRenderParcelsOverride;

        /// <summary>
        /// Gets or sets a value indicating whether to render parcels.
        /// </summary>
        public bool RenderParcels {
            get => m_ShouldRenderParcels;
            set => m_ShouldRenderParcels = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to override render parcels (i.e. for tool).
        /// </summary>
        public bool RenderParcelsOverride {
            get => m_ShouldRenderParcelsOverride;
            set => m_ShouldRenderParcelsOverride = value;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_OverlaySystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_ParcelQuery = SystemAPI.QueryBuilder()
                                     .WithAll<Parcel>()
                                     .WithNone<Deleted>()
                                     .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_ZoneCacheSystem     = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!m_ShouldRenderParcels && !m_ShouldRenderParcelsOverride) {
                return;
            }

            try {
                if (Camera.main is null) {
                    throw new NullReferenceException("Camera.main is null");
                }

                var edgeColors = m_ZoneCacheSystem.FillColors;
                var colorsMap  = new NativeHashMap<ushort, Color>(edgeColors.Count, Allocator.TempJob);

                foreach (var entry in edgeColors) {
                    colorsMap.Add(entry.Key, entry.Value);
                }

                // Todo split position calc, culling/render calc, and render step, into separate
                // jobs for perf
                var drawOverlaysJobData = new DrawOverlaysJob(
                    m_OverlayRenderSystem.GetBuffer(out var overlayRenderBufferJobHandle),
                    colorsMap,
                    SystemAPI.GetEntityTypeHandle(),
                    SystemAPI.GetComponentTypeHandle<Transform>(),
                    SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    SystemAPI.GetComponentTypeHandle<Parcel>(),
                    SystemAPI.GetComponentLookup<ParcelData>(),
                    SystemAPI.GetComponentLookup<ParcelSpawnable>(),
                    m_PreCullingSystem
                    .GetCullingData(true, out var cullingDataJobHandle),
                    SystemAPI.GetComponentTypeHandle<CullingInfo>());

                var drawOverlaysJob = drawOverlaysJobData.ScheduleByRef(
                    m_ParcelQuery,
                    JobHandle.CombineDependencies(
                        base.Dependency,
                        overlayRenderBufferJobHandle,
                        cullingDataJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawOverlaysJob);
                m_PreCullingSystem.AddCullingDataReader(drawOverlaysJob);

                drawOverlaysJob.Complete();
                colorsMap.Dispose();

                base.Dependency = drawOverlaysJob;
            } catch (Exception ex) {
                m_Log.Error($"Failed on DrawOverlaysJob:\n{ex}");
            }
        }

        /// <summary>
        /// Job to draw parcel overlays.
        /// </summary>
        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly] private OverlayRenderSystem.Buffer       m_OverlayRenderBuffer;
            [ReadOnly] private NativeHashMap<ushort, Color>     m_ColorsMap;
            [ReadOnly] private EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Transform>   m_TransformComponentTypeHandle;
            [ReadOnly] private ComponentTypeHandle<PrefabRef>   m_PrefabRefComponentTypeHandle;
            [ReadOnly] private ComponentTypeHandle<Parcel>      m_ParcelComponentTypeHandle;
            [ReadOnly] private ComponentLookup<ParcelData>      m_ParcelDataComponentLookup;
            [ReadOnly] private ComponentLookup<ParcelSpawnable> m_ParcelSpawnableComponentLookup;
            [ReadOnly] private NativeList<PreCullingData>       m_CullingData;
            [ReadOnly] private ComponentTypeHandle<CullingInfo> m_CullingInfoComponentTypeHandle;

            public DrawOverlaysJob(
                OverlayRenderSystem.Buffer       overlayRenderBuffer,
                NativeHashMap<ushort, Color>     colorsMap,
                EntityTypeHandle                 entityTypeHandle,
                ComponentTypeHandle<Transform>   transformComponentTypeHandle,
                ComponentTypeHandle<PrefabRef>   prefabRefComponentTypeHandle,
                ComponentTypeHandle<Parcel>      parcelComponentTypeHandle,
                ComponentLookup<ParcelData>      parcelDataComponentLookup,
                ComponentLookup<ParcelSpawnable> parcelSpawnableComponentLookup,
                NativeList<PreCullingData>       cullingData,
                ComponentTypeHandle<CullingInfo> cullingInfoComponentTypeHandle
            ) {
                m_OverlayRenderBuffer            = overlayRenderBuffer;
                m_ColorsMap                      = colorsMap;
                m_EntityTypeHandle               = entityTypeHandle;
                m_TransformComponentTypeHandle   = transformComponentTypeHandle;
                m_PrefabRefComponentTypeHandle   = prefabRefComponentTypeHandle;
                m_ParcelComponentTypeHandle      = parcelComponentTypeHandle;
                m_ParcelDataComponentLookup      = parcelDataComponentLookup;
                m_ParcelSpawnableComponentLookup = parcelSpawnableComponentLookup;
                m_CullingData                    = cullingData;
                m_CullingInfoComponentTypeHandle = cullingInfoComponentTypeHandle;
            }

            /// <inheritdoc/>
            public void Execute(
                    in ArchetypeChunk chunk,
                    int               unfilteredChunkIndex,
                    bool              useEnabledMask,
                    in v128           chunkEnabledMask) {
                var enumerator       = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var entitiesArray    = chunk.GetNativeArray(m_EntityTypeHandle);
                var transformsArray  = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);
                var prefabRefsArray  = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var parcelsArray     = chunk.GetNativeArray(ref m_ParcelComponentTypeHandle);
                var cullingInfoArray = chunk.GetNativeArray(ref m_CullingInfoComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var entity      = entitiesArray[i];
                    var cullingInfo = cullingInfoArray[i];

                    if (!IsNearCamera(cullingInfo)) {
                        continue;
                    }

                    var transform = transformsArray[i];
                    var prefabRef = prefabRefsArray[i];
                    var parcel    = parcelsArray[i];
                    var trs       = ParcelUtils.GetTransformMatrix(transform);

                    if (!m_ParcelDataComponentLookup.TryGetComponent(prefabRef, out var parcelData)) {
                        continue;
                    }

                    var spawnable = m_ParcelSpawnableComponentLookup.HasComponent(entity);

                    // Combines the translation part of the trs matrix (c3.xyz) with the local
                    // center to calculate the cube's world position.
                    DrawingUtils.DrawParcel(
                        m_OverlayRenderBuffer,
                        parcelData.m_LotSize,
                        trs,
                        m_ColorsMap[parcel.m_PreZoneType.m_Index],
                        spawnable
                    );
                }
            }

            private bool IsNearCamera(CullingInfo cullingInfo) {
                return cullingInfo.m_CullingIndex != 0 &&
                       (m_CullingData[cullingInfo.m_CullingIndex].m_Flags &
                        PreCullingFlags.NearCamera) > 0U;
            }
        }
    }
}