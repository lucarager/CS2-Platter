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

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelQuery;
        private EntityQuery m_ZoneQuery;

        // Data
        private Dictionary<ZoneType, Color> m_FillColors;
        private Dictionary<ZoneType, Color> m_EdgeColors;
        private bool m_ShouldRenderParcels = true;
        private bool m_ShouldRenderParcelsOverride = false;
        private bool m_UpdateColors;

        public bool RenderParcels {
            get => m_ShouldRenderParcels;
            set => m_ShouldRenderParcels = value;
        }

        public bool RenderParcelsOverride {
            get => m_ShouldRenderParcelsOverride;
            set => m_ShouldRenderParcelsOverride = value;
        }

        public bool TryGetZoneColor(ZoneType zoneType, out Color value) {
            var valid = m_EdgeColors.TryGetValue(zoneType, out var color);
            value = color;
            return valid;
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

            m_ZoneQuery = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<ZoneData>(),
                ComponentType.ReadOnly<PrefabData>(),
                ComponentType.Exclude<Deleted>(),
            });

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Color Data
            m_FillColors = new Dictionary<ZoneType, Color>();
            m_EdgeColors = new Dictionary<ZoneType, Color>();
        }

        private void UpdateZoneColors() {
            m_UpdateColors = false;
            var entities = m_ZoneQuery.ToEntityArray(Allocator.TempJob);

            for (var i = 0; i < entities.Length; i++) {
                var zonePrefabEntity = entities[i];
                var prefabData = EntityManager.GetComponentData<PrefabData>(zonePrefabEntity);
                var zoneData = EntityManager.GetComponentData<ZoneData>(zonePrefabEntity);
                var zonePrefab = m_PrefabSystem.GetPrefab<ZonePrefab>(prefabData);
                m_FillColors.Add(zoneData.m_ZoneType, zonePrefab.m_Color);
                m_EdgeColors.Add(zoneData.m_ZoneType, zonePrefab.m_Edge);
            }

            entities.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            m_Log.Debug($"OnGamePreload({purpose}, {mode})");

            if (m_FillColors.Count == 0) {
                m_UpdateColors = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!m_ShouldRenderParcels && !m_ShouldRenderParcelsOverride) {
                return;
            }

            if (!m_ZoneQuery.IsEmptyIgnoreFilter && m_UpdateColors) {
                UpdateZoneColors();
            }

            try {
                if (Camera.main is null) {
                    throw new NullReferenceException("Camera.main is null");
                }

                var buffer = m_OverlayRenderSystem.GetBuffer(out var overlayRenderBufferHandle);
                var colorsMap = new NativeHashMap<ZoneType, Color>(m_EdgeColors.Count, Allocator.TempJob);

                foreach (var entry in m_EdgeColors) {
                    colorsMap.Add(entry.Key, entry.Value);
                }

                // Todo split position calc, render calc, and render step, into separate jobs for perf
                var drawOverlaysJobData = new DrawOverlaysJob() {
                    m_OverlayRenderBuffer = buffer,
                    m_CameraPosition = (float3)Camera.main.transform.position,
                    m_ColorArray = colorsMap,
                    m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    m_TransformComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Objects.Transform>(),
                    m_PrefabRefComponentTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(),
                    m_ParcelComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Parcel>(),
                    m_ParcelDataComponentLookup = SystemAPI.GetComponentLookup<ParcelData>(),
                    m_ObjectGeometryComponentLookup = SystemAPI.GetComponentLookup<ObjectGeometryData>(),
                    m_ParcelSpawnableComponentLookup = SystemAPI.GetComponentLookup<ParcelSpawnable>(),
                    m_TempComponentLookup = SystemAPI.GetComponentLookup<Temp>(),
                };
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
                    if (isTemp && m_ColorArray[parcel.m_PreZoneType] != null) {
                        DrawingUtils.DrawParcel(m_OverlayRenderBuffer, parcelData.m_LotSize, trs, m_ColorArray[parcel.m_PreZoneType], spawnable);
                    } else {
                        DrawingUtils.DrawParcel(m_OverlayRenderBuffer, parcelData.m_LotSize, trs, spawnable);
                    }
                }
            }
        }
    }
}
