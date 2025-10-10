// <copyright file="P_OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Overlay Rendering System.
    /// <todo>Add culling and burst</todo>
    /// </summary>
    public partial class P_OverlaySystem : GameSystemBase {
        /// <summary>
        /// Instance.
        /// </summary>
        public static P_OverlaySystem Instance;

        // Systems & References
        private OverlayRenderSystem m_OverlayRenderSystem;
        private PrefabSystem m_PrefabSystem;
        private P_ZoneCacheSystem m_ZoneCacheSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelQuery;

        // Data
        private bool m_ShouldRenderParcels = true;
        private bool m_ShouldRenderParcelsOverride = false;

        public bool RenderParcels {
            get => m_ShouldRenderParcels;
            set => m_ShouldRenderParcels = value;
        }

        public bool RenderParcelsOverride {
            get => m_ShouldRenderParcelsOverride;
            set => m_ShouldRenderParcelsOverride = value;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_OverlaySystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_ParcelQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>(),
                    },
                    None = new ComponentType[] {
                        ComponentType.ReadOnly<Deleted>(),
                    },
                });

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ZoneCacheSystem = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();
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

                var buffer = m_OverlayRenderSystem.GetBuffer(out var overlayRenderBufferHandle);
                var edgeColors = m_ZoneCacheSystem.EdgeColors;
                var colorsMap = new NativeHashMap<ZoneType, Color>(edgeColors.Count, Allocator.TempJob);

                foreach (var entry in edgeColors) {
                    colorsMap.Add(entry.Key, entry.Value);
                }

                // Todo split position calc, render calc, and render step, into separate jobs for perf
                var drawOverlaysJobData = new DrawOverlaysJob(
                    overlayRenderBuffer: buffer,
                    cameraPosition: (float3)Camera.main.transform.position,
                    colorArray: colorsMap,
                    entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
                    transformComponentTypeHandle: SystemAPI.GetComponentTypeHandle<Game.Objects.Transform>(),
                    prefabRefComponentTypeHandle: SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    parcelComponentTypeHandle: SystemAPI.GetComponentTypeHandle<Parcel>(),
                    parcelDataComponentLookup: SystemAPI.GetComponentLookup<ParcelData>(),
                    objectGeometryComponentLookup: SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                    parcelSpawnableComponentLookup: SystemAPI.GetComponentLookup<ParcelSpawnable>(),
                    tempComponentLookup: SystemAPI.GetComponentLookup<Temp>()
                );
                var drawOverlaysJob = drawOverlaysJobData.ScheduleByRef(m_ParcelQuery, overlayRenderBufferHandle);

                m_OverlayRenderSystem.AddBufferWriter(drawOverlaysJob);
                drawOverlaysJob.Complete();
                colorsMap.Dispose();
                Dependency = drawOverlaysJob;
            } catch (Exception ex) {
                m_Log.Error($"Failed on DrawOverlaysJob:\n{ex}");
            }
        }

        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly]
            public OverlayRenderSystem.Buffer m_OverlayRenderBuffer;
            [ReadOnly]
            public float3 m_CameraPosition;
            [ReadOnly]
            public NativeHashMap<ZoneType, Color> m_ColorArray;
            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<Game.Objects.Transform> m_TransformComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefComponentTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<Parcel> m_ParcelComponentTypeHandle;
            [ReadOnly]
            public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;
            [ReadOnly]
            public ComponentLookup<ObjectGeometryData> m_ObjectGeometryComponentLookup;
            [ReadOnly]
            public ComponentLookup<ParcelSpawnable> m_ParcelSpawnableComponentLookup;
            [ReadOnly]
            public ComponentLookup<Temp> m_TempComponentLookup;

            public DrawOverlaysJob(OverlayRenderSystem.Buffer overlayRenderBuffer, float3 cameraPosition, NativeHashMap<ZoneType, Color> colorArray, EntityTypeHandle entityTypeHandle, ComponentTypeHandle<Game.Objects.Transform> transformComponentTypeHandle, ComponentTypeHandle<PrefabRef> prefabRefComponentTypeHandle, ComponentTypeHandle<Parcel> parcelComponentTypeHandle, ComponentLookup<ParcelData> parcelDataComponentLookup, ComponentLookup<ObjectGeometryData> objectGeometryComponentLookup, ComponentLookup<ParcelSpawnable> parcelSpawnableComponentLookup, ComponentLookup<Temp> tempComponentLookup) {
                m_OverlayRenderBuffer = overlayRenderBuffer;
                m_CameraPosition = cameraPosition;
                m_ColorArray = colorArray;
                m_EntityTypeHandle = entityTypeHandle;
                m_TransformComponentTypeHandle = transformComponentTypeHandle;
                m_PrefabRefComponentTypeHandle = prefabRefComponentTypeHandle;
                m_ParcelComponentTypeHandle = parcelComponentTypeHandle;
                m_ParcelDataComponentLookup = parcelDataComponentLookup;
                m_ObjectGeometryComponentLookup = objectGeometryComponentLookup;
                m_ParcelSpawnableComponentLookup = parcelSpawnableComponentLookup;
                m_TempComponentLookup = tempComponentLookup;
            }

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var entitiesArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var transformsArray = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);
                var prefabRefsArray = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);
                var parcelsArray = chunk.GetNativeArray(ref m_ParcelComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var entity = entitiesArray[i];
                    var transform = transformsArray[i];
                    var prefabRef = prefabRefsArray[i];
                    var parcel = parcelsArray[i];
                    var trs = ParcelUtils.GetTransformMatrix(transform);

                    if (!m_ParcelDataComponentLookup.TryGetComponent(prefabRef, out var parcelData)) {
                        return;
                    }

                    var isTemp = m_TempComponentLookup.HasComponent(entity);
                    var spawnable = m_ParcelSpawnableComponentLookup.HasComponent(entity);

                    // Combines the translation part of the trs matrix (c3.xyz) with the local center to calculate the cube's world position.
                    if (m_ColorArray[parcel.m_PreZoneType] != null) {
                        DrawingUtils.DrawParcel(m_OverlayRenderBuffer, parcelData.m_LotSize, trs, m_ColorArray[parcel.m_PreZoneType], spawnable);
                    } else {
                        DrawingUtils.DrawParcel(m_OverlayRenderBuffer, parcelData.m_LotSize, trs, spawnable);
                    }
                }
            }
        }
    }
}
