// <copyright file="P_OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Colossal.Mathematics;
    using Components;
    using Game;
    using Game.Common;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Game.Zones;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using FrustumPlanes = Game.Rendering.FrustumPlanes;
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

            RequireForUpdate(m_ParcelQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (!ShouldRenderOverlay() || m_ParcelQuery.IsEmpty) {
                return;
            }

            try {
                if (Camera.main is null) {
                    throw new NullReferenceException("Camera.main is null");
                }

                // Build SOA frustum plane packets for per-parcel culling
                var managedPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
                var nativePlanes  = new NativeArray<Plane>(6, Allocator.TempJob);
                nativePlanes.CopyFrom(managedPlanes);
                var planePackets  = new NativeList<FrustumPlanes.PlanePacket4>(2, Allocator.TempJob);
                FrustumPlanes.BuildSOAPlanePackets(nativePlanes, 6, planePackets);
                nativePlanes.Dispose();

                var edgeColors = m_ZoneCacheSystem.FillColors;
                var colorsMap  = new NativeHashMap<ushort, Color>(edgeColors.Count, Allocator.TempJob);

                foreach (var entry in edgeColors) {
                    colorsMap.Add(entry.Key, entry.Value);
                }

                // Prepare: parallel geometry math → command stream
                var chunkCount    = m_ParcelQuery.CalculateChunkCountWithoutFiltering();
                var commandStream = new NativeStream(chunkCount, Allocator.TempJob);

                var prepareJob = new PrepareOverlayCommandsJob {
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
                    m_CommandWriter                  = commandStream.AsWriter(),
                    m_CullingPlanes                  = planePackets.AsArray(),
                };

                var prepareHandle = prepareJob.ScheduleParallel(m_ParcelQuery, Dependency);

                // Render: sequential dispatch of pre-computed commands to the overlay buffer
                var overlayBuffer = m_OverlayRenderSystem.GetBuffer(out var bufferDeps);

                var renderJob = new RenderOverlayCommandsJob {
                    m_OverlayRenderBuffer = overlayBuffer,
                    m_CommandReader       = commandStream.AsReader(),
                    m_ForEachCount        = chunkCount,
                };

                var renderHandle = renderJob.Schedule(JobHandle.CombineDependencies(prepareHandle, bufferDeps));

                m_OverlayRenderSystem.AddBufferWriter(renderHandle);
                colorsMap.Dispose(renderHandle);
                commandStream.Dispose(renderHandle);
                planePackets.Dispose(renderHandle);

                Dependency = renderHandle;
            } catch (Exception ex) {
                m_Log.Error($"Failed on overlay jobs:\n{ex}");
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
    }
}