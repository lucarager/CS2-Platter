// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using System;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Components;
    using Extensions;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using Block = Game.Zones.Block;
    using SearchSystem = Game.Net.SearchSystem;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Ovverides object placement to snap parcels to road sides 
    /// </summary>
    public partial class P_SnapSystem : PlatterGameSystemBase {
        [Flags]
        public enum SnapMode : uint {
            None             = 0,
            ZoneSide         = 1,
            RoadSide         = 2,
            ParcelEdge       = 4,
            ParcelFrontAlign = 8,
        }

        private static class SnapLevel {
            public const float None             = 0f;
            public const float ParcelEdge       = 1.5f;
            public const float RoadSide         = 2f;
            public const float ParcelFrontAlign = 2.5f;
            public const float ZoneSide         = 2.5f;
        }

        private EntityQuery m_Query;
        private float       m_SnapSetback;

        public  NativeReference<bool>   IsSnapped;
        private ObjectToolSystem        m_ObjectToolSystem;
        private P_ParcelSearchSystem    m_ParcelSearchSystem;
        private SearchSystem            m_NetSearchSystem;
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private SnapMode                m_SnapMode;
        private TerrainSystem           m_TerrainSystem;
        private ToolSystem              m_ToolSystem;
        private WaterSystem             m_WaterSystem;

        public float CurrentSnapSetback {
            get => m_SnapSetback;
            set {
                m_SnapSetback = value;
                m_ObjectToolSystem.SetMemberValue("m_ForceUpdate", true);
            }
        }

        public static float DefaultSnapDistance => MinSnapDistance;
        public static float MaxSnapDistance     => 8f;
        public static float MinSnapDistance     => 0f;

        // Props
        public SnapMode CurrentSnapMode {
            get => m_SnapMode;
            set => m_SnapMode = value;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneSearchSystem   = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_NetSearchSystem    = World.GetOrCreateSystemManaged<SearchSystem>();
            m_ParcelSearchSystem = World.GetOrCreateSystemManaged<P_ParcelSearchSystem>();
            m_ObjectToolSystem   = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_ToolSystem         = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem      = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem        = World.GetOrCreateSystemManaged<WaterSystem>();

            // Query
            m_Query = SystemAPI.QueryBuilder()
                               .WithAllRW<ObjectDefinition>()
                               .WithAll<CreationDefinition, Updated>()
                               .WithNone<Deleted, Overridden>()
                               .Build();

            // Data
            m_SnapSetback   = DefaultSnapDistance;
            m_SnapMode      = SnapMode.RoadSide | SnapMode.ParcelFrontAlign;
            IsSnapped       = new NativeReference<bool>(Allocator.Persistent);
            IsSnapped.Value = false;

            RequireForUpdate(m_Query);
        }

        protected override void OnDestroy() {
            IsSnapped.Dispose();
            base.OnDestroy();
        }

        public bool ShouldCustomSnap() {
            // Advanced Line Tool
            //var isALTool  = m_ToolSystem.activeTool.toolID == "Line Tool" && m_ToolSystem.activePrefab is ParcelPlaceholderPrefab;
            // Vanilla Object Tool
            var isObjTool = m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPlaceholderPrefab;

            return isObjTool;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            IsSnapped.Value = false;

            // Exit early on certain conditions
            if (m_Query.IsEmptyIgnoreFilter || !ShouldCustomSnap()) {
                return;
            }

            // Handle vanilla line tool when not in individual plop mode
            if (m_ObjectToolSystem.actualMode is not ObjectToolSystem.Mode.Create &&
                m_ObjectToolSystem.prefab is ParcelPlaceholderPrefab parcelPrefab) {
                // Override distance scale
                // ObjectToolSystem calculates distance between objects by taking distanceScale and multiplying it
                // by distance, which is based on the in-game slider, ranging from 1.5f to 6f.
                // By dividing the lot width by 1.5f, we ensure that the minimum distance on the slider creates an edge-to-edge placement.
                var width = (parcelPrefab.m_LotWidth * 8f) / 1.5f;
                m_ObjectToolSystem.SetMemberValue("distanceScale", width);
                // Patch an edge case where `distance` is set to 1.4f or lower, causing incorrect placement.
                var currentScaleMult = (float)m_ObjectToolSystem.GetMemberValue("distance");
                if (currentScaleMult < 1.6f) {
                    m_ObjectToolSystem.SetMemberValue("distance", 1.5f);
                }
            }

            // Exit on disabled snap
            if (m_SnapMode == SnapMode.None) {
                return;
            }

            //// Grab control points from ObjectTool
            //var controlPoints = m_ObjectToolSystem.GetControlPoints(out var deps);
            //Dependency = JobHandle.CombineDependencies(Dependency, deps);

            //// If none, exit
            //if (controlPoints.Length == 0) {
            //    return;
            //}

            //var curvesList   = new NativeList<Bezier4x3>(Allocator.Temp);
            //var curvesFilter = new NativeList<bool>(Allocator.Temp);

            //// Schedule our snapping job
            //var parcelSnapJobHandle = new ParcelSnapJob
            //{
            //    m_ZoneTree                     = m_ZoneSearchSystem.GetSearchTree(true, out var zoneTreeJobHandle),
            //    m_NetTree                      = m_NetSearchSystem.GetNetSearchTree(true, out var netTreeJobHandle),
            //    m_ParcelTree                   = m_ParcelSearchSystem.GetStaticSearchTree(true, out var parcelTreeJobHandle),
            //    m_CurvesList                   = curvesList,
            //    m_CurvesFilter                 = curvesFilter,
            //    m_SnapMode                     = m_SnapMode,
            //    m_ControlPoints                = controlPoints,
            //    m_ObjectDefinitionTypeHandle   = SystemAPI.GetComponentTypeHandle<ObjectDefinition>(),
            //    m_CreationDefinitionTypeHandle = SystemAPI.GetComponentTypeHandle<CreationDefinition>(true),
            //    m_BlockComponentLookup         = SystemAPI.GetComponentLookup<Block>(true),
            //    m_ParcelDataComponentLookup    = SystemAPI.GetComponentLookup<ParcelData>(true),
            //    m_ParcelOwnerComponentLookup   = SystemAPI.GetComponentLookup<ParcelOwner>(true),
            //    m_TransformComponentLookup     = SystemAPI.GetComponentLookup<Transform>(true),
            //    m_ParcelComponentLookup        = SystemAPI.GetComponentLookup<Parcel>(true),
            //    m_NodeLookup                   = SystemAPI.GetComponentLookup<Node>(true),
            //    m_EdgeLookup                   = SystemAPI.GetComponentLookup<Edge>(true),
            //    m_CurveLookup                  = SystemAPI.GetComponentLookup<Curve>(true),
            //    m_CompositionLookup            = SystemAPI.GetComponentLookup<Composition>(true),
            //    m_PrefabRefLookup              = SystemAPI.GetComponentLookup<PrefabRef>(true),
            //    m_NetDataLookup                = SystemAPI.GetComponentLookup<NetData>(true),
            //    m_NetGeometryDataLookup        = SystemAPI.GetComponentLookup<NetGeometryData>(true),
            //    m_NetCompositionDataLookup     = SystemAPI.GetComponentLookup<NetCompositionData>(true),
            //    m_EdgeGeoLookup                = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
            //    m_StartNodeGeoLookup           = SystemAPI.GetComponentLookup<StartNodeGeometry>(true),
            //    m_EndNodeGeoLookup             = SystemAPI.GetComponentLookup<EndNodeGeometry>(true),
            //    m_ConnectedEdgeLookup          = SystemAPI.GetBufferLookup<ConnectedEdge>(),
            //    m_TerrainHeightData            = m_TerrainSystem.GetHeightData(),
            //    m_WaterSurfaceData             = m_WaterSystem.GetSurfaceData(out var waterSurfaceJobHandle),
            //    m_SnapSetback                  = m_SnapSetback,
            //    m_EntityTypeHandle             = SystemAPI.GetEntityTypeHandle(),
            //    m_ConnectedParcelLookup        = SystemAPI.GetBufferLookup<ConnectedParcel>(true),
            //    m_IsSnapped                    = IsSnapped,
            //}.ScheduleParallel(
            //    m_Query,
            //    JobUtils.CombineDependencies(Dependency, zoneTreeJobHandle, netTreeJobHandle, waterSurfaceJobHandle, parcelTreeJobHandle)
            //);

            //m_ZoneSearchSystem.AddSearchTreeReader(parcelSnapJobHandle);
            //m_NetSearchSystem.AddNetSearchTreeReader(parcelSnapJobHandle);
            //m_ParcelSearchSystem.AddSearchTreeReader(parcelSnapJobHandle);
            //m_TerrainSystem.AddCPUHeightReader(parcelSnapJobHandle);
            //m_WaterSystem.AddSurfaceReader(parcelSnapJobHandle);

            //curvesList.Dispose(parcelSnapJobHandle);
            //curvesFilter.Dispose(parcelSnapJobHandle);

            //Dependency = JobHandle.CombineDependencies(Dependency, parcelSnapJobHandle);
        }
    }
}