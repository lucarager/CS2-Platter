// <copyright file="PlatterToolSystem.Definitions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using Colossal.Annotations;
    using Colossal.Collections;
    using Game;
    using Game.City;
    using Game.Common;
    using Game.Input;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "Parcel Tool";

        /// <summary>
        /// Instance.
        /// </summary>
        public static PlatterToolSystem Instance;

        // Logger
        private PrefixedLogger m_Log;

        // Systems & References
        private Game.Common.RaycastSystem m_RaycastSystem;
        private CameraUpdateSystem m_CameraUpdateSystem;
        private PlatterOverlaySystem m_PlatterOverlaySystem;
        private CityConfigurationSystem m_CityConfigurationSystem;
        private OverlayRenderSystem.Buffer m_OverlayBuffer;
        private Game.Net.SearchSystem m_NetSearchSystem;
        private Game.Zones.SearchSystem m_ZoneSearchSystem;

        // Jobs
        private JobHandle m_InputDeps;

        // Actions
        private ProxyAction m_ApplyAction;
        private ProxyAction m_CreateAction;
        private ProxyAction m_CancelAction;

        // Queries
        private EntityQuery m_HighlightedQuery;
        private EntityQuery m_DefinitionQuery;

        // Prefab selection
        private ToolBaseSystem m_PreviousTool = null;
        private ObjectPrefab m_SelectedPrefab;
        private Entity m_SelectedEntity = Entity.Null;

        // Data
        private int2 m_SelectedParcelSize = new(2, 2);
        private ObjectPrefab m_Prefab;
        private TransformPrefab m_TransformPrefab;
        private ControlPoint m_LastRaycastPoint;
        private NativeValue<Rotation> m_Rotation;
        private NativeList<ControlPoint> m_ControlPoints;
        private bool m_ForceCancel;
        private CameraController m_CameraController;
        private Entity m_SelectedEdgeEntity;
        private PrefabBase m_SelectedEdgePrefabBase;
        public Entity m_HoveredEntity;
        public float3 m_LastHitPosition;
        private TerrainHeightData m_TerrainHeightData;
        public List<Transform> m_Points;
        private RandomSeed m_RandomSeed;
        private float m_RoadEditorSpacing = 1f;
        private float m_RoadEditorOffset = 2f;
        private bool4 m_RoadEditorSides = new bool4(true, true, false, false);

        // Mode Data
        private ToolData_Plop m_PlopData;
        private ToolData_RoadEditor m_RoadEditorData;
        private PlatterToolMode m_CurrentMode = PlatterToolMode.Plop;

        public int2 SelectedParcelSize {
            get => this.m_SelectedParcelSize;
            set => this.m_SelectedParcelSize = value;
        }

        public float RoadEditorSpacing {
            get => m_RoadEditorSpacing;
            set => m_RoadEditorSpacing = value;
        }

        public float RoadEditorOffset {
            get => m_RoadEditorOffset;
            set => m_RoadEditorOffset = value;
        }

        public bool4 RoadEditorSides {
            get => m_RoadEditorSides;
            set => m_RoadEditorSides = value;
        }

        public ZoneType PreZoneType {
            get; set;
        }

        private struct Rotation {
            public quaternion m_Rotation;

            public quaternion m_ParentRotation;

            public bool m_IsAligned;
        }

        [CanBeNull]
        public ObjectPrefab SelectedPrefab {
            get => this.m_SelectedPrefab;
            set {
                m_SelectedPrefab = value as ObjectGeometryPrefab;
                m_SelectedEntity = m_SelectedPrefab is null ? Entity.Null : m_PrefabSystem.GetEntity(m_SelectedPrefab);
            }
        }

        public TransformPrefab Transform {
            get => m_TransformPrefab;
            set {
                if (value != m_TransformPrefab) {
                    m_TransformPrefab = value;
                    m_ForceUpdate = true;
                    if (value != null) {
                        m_SelectedPrefab = null;
                        var eventPrefabChanged = m_ToolSystem.EventPrefabChanged;
                        if (eventPrefabChanged == null) {
                            return;
                        }

                        eventPrefabChanged(value);
                    }
                }
            }
        }

        public enum PlatterToolMode {
            Plop = 0,
            Brush = 1,
            RoadEdge = 2,
        }

        public PlatterToolMode CurrentMode {
            get => m_CurrentMode;

            set {
                m_Log.Debug($"CurrentMode.Set(mode = {value}) -- m_CurrentMode {m_CurrentMode}");

                // Don't do anything if no change.
                if (value == m_CurrentMode) {
                    return;
                }

                // Apply updated tool mode.
                switch (value) {
                    case PlatterToolMode.Plop:
                        m_PlopData = new ToolData_Plop(EntityManager, m_PrefabSystem);
                        break;
                    case PlatterToolMode.RoadEdge:
                        m_RoadEditorData = new ToolData_RoadEditor(EntityManager, m_PrefabSystem);
                        break;
                }

                // Update mode.
                m_CurrentMode = value;
            }
        }

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }

        public virtual Snap SelectedSnap {
            get; set;
        }

        /// <inheritdoc/>
        public override PrefabBase GetPrefab() {
            return m_SelectedPrefab;
        }
    }
}
