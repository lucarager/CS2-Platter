// <copyright file="RoadCurveToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.City;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Platter.Settings;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using static Platter.Systems.PlatterToolSystem;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class RoadCurveToolSystem : ObjectToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "Road Curve Tool";

        /// <summary>
        /// Instance.
        /// </summary>
        public static RoadCurveToolSystem Instance;

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
        private ProxyAction m_CancelAction;

        // Queries
        private EntityQuery m_HighlightedQuery;

        // Prefab selection
        private ToolBaseSystem m_PreviousTool = null;

        // Data
        private ControlPoint m_LastRaycastPoint;
        private CameraController m_CameraController;
        private Entity m_SelectedEdgeEntity;
        private PrefabBase m_SelectedEdgePrefabBase;
        public Entity m_HoveredEntity;
        public float3 m_LastHitPosition;
        private TerrainHeightData m_TerrainHeightData;

        /// <inheritdoc/>
        public override PrefabBase GetPrefab() {
            return null;
        }

        /// <inheritdoc/>
        public override bool TrySetPrefab(PrefabBase prefab) {
            return false;
        }

        public void RequestEnable() {
            m_Log.Debug($"RequestEnable()");

            if (m_ToolSystem.activeTool != this) {
                m_PreviousTool = m_ToolSystem.activeTool;
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
            }
        }

        public void RequestDisable() {
            m_Log.Debug($"RequestDisable()");

            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            // References & State
            Instance = this;
            Enabled = false;

            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(PlatterToolSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_HighlightedQuery = GetEntityQuery(ComponentType.ReadOnly<Highlighted>());

            // Get Systems
            m_RaycastSystem = World.GetOrCreateSystemManaged<Game.Common.RaycastSystem>();
            m_CameraUpdateSystem = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_OverlayBuffer = World.GetOrCreateSystemManaged<OverlayRenderSystem>().GetBuffer(out var _);
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<PlatterOverlaySystem>();

            // Set apply and cancel actions from game settings.
            m_ApplyAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.ApplyActionName);
            m_CancelAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.CancelActionName);
        }

        /// <inheritdoc/>
        protected override void OnStartRunning() {
            m_Log.Debug($"OnStartRunning()");

            base.OnStartRunning();

            // Ensure apply action is enabled.
            m_ApplyAction.shouldBeEnabled = true;
            m_CancelAction.shouldBeEnabled = true;

            // Clear any previous raycast result.
            m_LastHitPosition = default;
        }

        /// <inheritdoc/>
        protected override void OnStopRunning() {
            m_Log.Debug($"OnStopRunning()");
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            m_Log.Debug($"OnDestroy()");

            base.OnDestroy();
        }

        /// <inheritdoc/>
        public override void InitializeRaycast() {
            base.InitializeRaycast();
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround;
            m_ToolRaycastSystem.typeMask = TypeMask.Lanes | TypeMask.Net;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger;
            m_ToolRaycastSystem.netLayerMask = Layer.Road;
            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            // Clear state.
            applyMode = ApplyMode.Clear;

            m_InputDeps = base.OnUpdate(inputDeps);

            if (GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                // Store results
                var previousHoveredEntity = m_HoveredEntity;
                m_HoveredEntity = entity;
                m_LastHitPosition = raycastHit.m_HitPosition;

                // Check for apply action initiation.
                var applyWasPressed = m_ApplyAction.WasPressedThisFrame() ||
                                     (m_ToolSystem.actionMode.IsEditor() && applyAction.WasPressedThisFrame());

                if (applyWasPressed) {
                    if (entity != m_SelectedEdgeEntity &&
                        EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef) &&
                        EntityManager.TryGetComponent<Curve>(entity, out var curve) &&
                        EntityManager.TryGetComponent<EdgeGeometry>(entity, out var edgeGeo) &&
                        EntityManager.TryGetComponent<Composition>(entity, out var composition) &&
                        EntityManager.TryGetComponent<NetCompositionData>(composition.m_Edge, out var edgeNetCompData) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase)) {
                        // Highlighting
                        SwapHighlitedEntities(m_SelectedEdgeEntity, entity);

                        // Store results
                        m_SelectedEdgeEntity = entity;

                        // m_ModeData.SelectedCurve = curve;
                        // m_ModeData.SelectedCurveGeo = edgeNetCompData;
                        // m_ModeData.SelectedCurveEdgeGeo = edgeGeo;
                        // m_ModeData.SelectedPrefabBase = prefabBase;
                    }
                } else if (previousHoveredEntity != m_HoveredEntity) {
                    // If we were previously hovering over an Edge that isn't the selected one, unhighlight it.
                    if (previousHoveredEntity != m_SelectedEdgeEntity) {
                        ChangeHighlighting(previousHoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                    }

                    // Highlight the hovered entity
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.AddHighlight);
                }
            } else if (m_CancelAction.WasPressedThisFrame() ||
                (m_SelectedEdgeEntity != Entity.Null && !EntityManager.Exists(m_SelectedEdgeEntity))) {
                // Right click & we had something selected -> deselect and reset the tool.
                ChangeHighlighting(m_SelectedEdgeEntity, ChangeObjectHighlightMode.RemoveHighlight);
                ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                m_SelectedEdgeEntity = Entity.Null;
                m_HoveredEntity = Entity.Null;
                m_LastHitPosition = float3.zero;
            } else if (m_HoveredEntity != Entity.Null) {
                // No raycast hit, no action pressed, remove hover from any entity that was being hovered before.
                if (m_HoveredEntity != m_SelectedEdgeEntity) {
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                }

                m_HoveredEntity = Entity.Null;
                m_LastHitPosition = float3.zero;
            }

            return inputDeps;
        }

        internal void SwapHighlitedEntities(Entity oldEntity, Entity newEntity) {
            ChangeHighlighting(oldEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(newEntity, ChangeObjectHighlightMode.AddHighlight);
        }

        internal void ChangeHighlighting(Entity entity, ChangeObjectHighlightMode mode) {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) {
                return;
            }

            bool wasChanged = false;

            if (mode == ChangeObjectHighlightMode.AddHighlight && !EntityManager.HasComponent<Highlighted>(entity)) {
                m_Log.Debug($"Highlighted {entity}");

                EntityManager.AddComponent<Highlighted>(entity);
                wasChanged = true;
            } else if (mode == ChangeObjectHighlightMode.RemoveHighlight && EntityManager.HasComponent<Highlighted>(entity)) {
                m_Log.Debug($"Unhighlighted {entity}");

                EntityManager.RemoveComponent<Highlighted>(entity);
                wasChanged = true;
            }

            if (wasChanged && !EntityManager.HasComponent<BatchesUpdated>(entity)) {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
        }
    }
}