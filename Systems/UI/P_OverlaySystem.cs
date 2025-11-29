// <copyright file="P_OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Components;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Rendering;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using UnityEngine;
    using Utils;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Overlay Rendering System.
    /// <todo>Check BuildingLotRenderJob for any optimizations to grab</todo>
    /// </summary>
    public partial class P_OverlaySystem : GameSystemBase {
        // Data
        private bool m_ShouldRenderParcelsOverride;

        // Queries
        private EntityQuery m_ParcelQuery;

        // Systems & References
        private OverlayRenderSystem m_OverlayRenderSystem;
        private P_ZoneCacheSystem   m_ZoneCacheSystem;
        private PreCullingSystem    m_PreCullingSystem;

        // Logger
        private PrefixedLogger m_Log;

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
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
            m_ZoneCacheSystem     = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!PlatterMod.Instance.Settings.RenderParcels && !m_ShouldRenderParcelsOverride) {
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
                var drawOverlaysJobData = new DrawOverlaysJob {
                    m_OverlayRenderBuffer            = m_OverlayRenderSystem.GetBuffer(out var overlayRenderBufferJobHandle),
                    m_ColorsMap                      = colorsMap,
                    m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                    m_TransformComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<Transform>(),
                    m_PrefabRefComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    m_ParcelComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<Parcel>(),
                    m_ParcelDataComponentLookup      = SystemAPI.GetComponentLookup<ParcelData>(),
                    m_ParcelSpawnableComponentLookup = SystemAPI.GetComponentLookup<ParcelSpawnable>(),
                    m_CullingData                    = m_PreCullingSystem.GetCullingData(true, out var cullingDataJobHandle),
                    m_CullingInfoComponentTypeHandle = SystemAPI.GetComponentTypeHandle<CullingInfo>(),
                };

                var drawOverlaysJob = drawOverlaysJobData.ScheduleByRef(
                    m_ParcelQuery,
                    JobHandle.CombineDependencies(
                        Dependency,
                        overlayRenderBufferJobHandle,
                        cullingDataJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawOverlaysJob);
                m_PreCullingSystem.AddCullingDataReader(drawOverlaysJob);

                drawOverlaysJob.Complete();
                colorsMap.Dispose();

                Dependency = drawOverlaysJob;
            } catch (Exception ex) {
                m_Log.Error($"Failed on DrawOverlaysJob:\n{ex}");
            }
        }

        /// <summary>
        /// Job to draw parcel overlays.
        /// </summary>
        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer       m_OverlayRenderBuffer;
            [ReadOnly] public required NativeHashMap<ushort, Color>     m_ColorsMap;
            [ReadOnly] public required EntityTypeHandle                 m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Transform>   m_TransformComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<PrefabRef>   m_PrefabRefComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Parcel>      m_ParcelComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<ParcelData>      m_ParcelDataComponentLookup;
            [ReadOnly] public required ComponentLookup<ParcelSpawnable> m_ParcelSpawnableComponentLookup;
            [ReadOnly] public required NativeList<PreCullingData>       m_CullingData;
            [ReadOnly] public required ComponentTypeHandle<CullingInfo> m_CullingInfoComponentTypeHandle;

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
                var cullingInfoArray = chunk.GetNativeArray(ref m_CullingInfoComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var entity      = entitiesArray[i];
                    var cullingInfo = cullingInfoArray[i];

                    // Temporary fix until we can figure out how to avoid culling overridden Parcels
                    //if (!IsNearCamera(cullingInfo)) {
                    //    continue;
                    //}

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
                return cullingInfo.m_CullingIndex != 0 && (m_CullingData[cullingInfo.m_CullingIndex].m_Flags & PreCullingFlags.NearCamera) > 0U;
            }
        }
    }
}