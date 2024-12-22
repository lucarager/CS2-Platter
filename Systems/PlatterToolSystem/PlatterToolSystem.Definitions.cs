// <copyright file="PlatterToolSystem.Definitions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Collections.Generic;
    using Colossal.Annotations;
    using Game;
    using Game.City;
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

        // Jobs
        private JobHandle m_InputDeps;

        // Actions
        private ProxyAction m_ApplyAction;
        private ProxyAction m_SecondaryApplyAction;
        private ProxyAction m_CancelAction;

        // Queries
        private EntityQuery m_HighlightedQuery;

        // Prefab selection
        private ToolBaseSystem m_PreviousTool = null;
        private ObjectPrefab m_SelectedPrefab;
        private Entity m_SelectedEntity = Entity.Null;

        // Data
        private int2 m_SelectedParcelSize = new(2, 2);
        private float m_RoadEditorSpacing = 0f;
        private ObjectPrefab m_Prefab;
        private TransformPrefab m_Transform;
        private ControlPoint m_LastRaycastPoint;
        private NativeList<ControlPoint> m_ControlPoints;
        private bool m_ForceCancel;
        private CameraController m_CameraController;
        private Entity m_SelectedEdgeEntity;
        private PrefabBase m_SelectedEdgePrefabBase;
        public Entity m_HoveredEntity;
        public float3 m_LastHitPosition;
        private TerrainHeightData m_TerrainHeightData;
        private List<Transform> m_Points;

        // Mode.
        private PlatterToolModeData m_ModeData;
        private PlatterToolMode m_CurrentMode = PlatterToolMode.Point;

        public int2 SelectedParcelSize {
            get => this.m_SelectedParcelSize;
            set => this.m_SelectedParcelSize = value;
        }

        public float RoadEditorSpacing {
            get; set;
        }

        public float RoadEditorOffset {
            get; set;
        }

        public bool4 RoadEditorSides {
            get; set;
        }

        public ZoneType PreZoneType {
            get; set;
        }

        [CanBeNull]
        public ObjectPrefab SelectedPrefab {
            get => this.m_SelectedPrefab;
            set {
                m_SelectedPrefab = value as ObjectGeometryPrefab;
                m_SelectedEntity = m_SelectedPrefab is null ? Entity.Null : m_PrefabSystem.GetEntity(m_SelectedPrefab);
            }
        }

        /// <summary>
        /// Gets the currently selected entity.
        /// </summary>
        internal Entity SelectedEntity => m_SelectedEntity;

        public TransformPrefab Transform {
            get => m_Transform;
            set {
                if (value != m_Transform) {
                    m_Transform = value;
                    m_ForceUpdate = true;
                    if (value != null) {
                        m_SelectedPrefab = null;
                        Action<PrefabBase> eventPrefabChanged = m_ToolSystem.EventPrefabChanged;
                        if (eventPrefabChanged == null) {
                            return;
                        }

                        eventPrefabChanged(value);
                    }
                }
            }
        }

        public enum PlatterToolMode {
            Point = 0,
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
                    case PlatterToolMode.Point:
                        m_ModeData = new RoadEdgeData();
                        break;
                    case PlatterToolMode.Brush:
                        m_ModeData = new RoadEdgeData();
                        break;
                    case PlatterToolMode.RoadEdge:
                        m_ModeData = new RoadEdgeData();
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
