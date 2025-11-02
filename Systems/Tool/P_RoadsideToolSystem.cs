// <copyright file="P_RoadsideToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Json;
using Game.Simulation;
using Platter.Settings;
using UnityEngine;

namespace Platter.Systems {
    using System;
    using System.Linq;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Constants;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.City;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Pathfind;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Utils;
    using Color = UnityEngine.Color;
    using NetSearchSystem = Game.Net.SearchSystem;
    using Random = Unity.Mathematics.Random;
    using ZoneSearchSystem = Game.Zones.SearchSystem;

    /// <summary>
    /// New in-progress system to handle plopping parcels directly.
    /// Currently not enabled.
    /// </summary>
    public partial class P_RoadsideToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Instance.
        /// </summary>
        public static P_RoadsideToolSystem Instance;

        public enum Mode {
            Create,
            Upgrade,
            Move,
        }

        public enum State {
            Default,
            Rotating,
            Adding,
            Removing,
        }

        public bool ApplyWasRequested;

        // Data
        private NativeReference<NetToolSystem.AppliedUpgrade> m_AppliedUpgrade;
        private bool                                          m_ApplyBlocked;
        private CameraController                              m_CameraController;
        private CameraUpdateSystem                            m_CameraUpdateSystem;
        private NativeList<ControlPoint>                      m_ControlPoints;
        private EntityQuery                                   m_DefinitionQuery;
        private bool                                          m_ForceCancel;

        // Queries
        private EntityQuery  m_HighlightedQuery;
        public  Entity       m_HoveredEntity;
        private float3       m_LastCursorPosition;
        public  float3       m_LastHitPosition;
        private ControlPoint m_LastRaycastPoint;
        private Mode         m_LastToolMode;

        // Logger
        private PrefixedLogger  m_Log;
        private Entity          m_MovingInitialized;
        public  List<Transform> m_Points;

        // Actions
        private IProxyAction m_PreciseRotation;
        private ParcelPrefab m_Prefab;
        private RandomSeed   m_RandomSeed;

        // Systems & References
        private RaycastSystem                          m_RaycastSystem;
        private ToolData_RoadEditor                    m_RoadEditorData;
        private float                                  m_RoadEditorOffset  = 2f;
        private bool4                                  m_RoadEditorSides   = new(true, true, false, false);
        private float                                  m_RoadEditorSpacing = 1f;
        private NativeReference<Rotation>              m_Rotation;
        private bool                                   m_RotationModified;
        private float3                                 m_RotationStartPosition;
        private Entity                                 m_SelectedEdgeEntity;
        private PrefabBase                             m_SelectedEdgePrefabBase;
        private Entity                                 m_SelectedEntity     = Entity.Null;
        private int2                                   m_SelectedParcelSize = new(2, 2);
        private ObjectPrefab                           m_SelectedPrefab;
        private Snap                                   m_SelectedSnap;
        private ControlPoint                           m_StartPoint;
        private quaternion                             m_StartRotation;
        private State                                  m_State;
        private EntityQuery                            m_TempQuery;
        private TerrainHeightData                      m_TerrainHeightData;
        private Mode                                   m_ToolMode;
        private TransformPrefab                        m_TransformPrefab;
        private NativeList<NetToolSystem.UpgradeState> m_UpgradeStates;
        private CityConfigurationSystem                m_CityConfigurationSystem;
        private OverlayRenderSystem.Buffer             m_OverlayBuffer;
        private ZoneSearchSystem                m_ZoneSearchSystem;
        private NetSearchSystem                        m_NetSearchSystem;
        private P_OverlaySystem                        m_PlatterOverlaySystem;

        // Actions
        private ProxyAction m_ApplyAction;
        private ProxyAction m_CreateAction;
        private ProxyAction m_CancelAction;

        public int2 SelectedParcelSize {
            get => m_SelectedParcelSize;
            set {
                m_SelectedParcelSize               = value;
                m_RoadEditorData.RecalculatePoints = true;
            }
        }

        public float RoadEditorSpacing {
            get => m_RoadEditorSpacing;
            set {
                m_RoadEditorSpacing                = value;
                m_RoadEditorData.RecalculatePoints = true;
            }
        }

        public float RoadEditorOffset {
            get => m_RoadEditorOffset;
            set {
                m_RoadEditorOffset                 = value;
                m_RoadEditorData.RecalculatePoints = true;
            }
        }

        public bool4 RoadEditorSides {
            get => m_RoadEditorSides;
            set {
                m_RoadEditorSides                  = value;
                m_RoadEditorData.RecalculatePoints = true;
            }
        }

        private bool isUpgradeMode {
            get {
                var flag = m_UpgradeStates.Length >= 1;
                if (!flag) {
                    return m_UpgradeStates.Length >= 1;
                }

                return m_ToolMode == Mode.Create;
            }
        }

        /// <inheritdoc/>
        public override string toolID => "ParcelTool";

        public override Snap selectedSnap {
            get => m_SelectedSnap;
            set {
                if (value == m_SelectedSnap) {
                    return;
                }

                m_SelectedSnap = value;
                m_ForceUpdate  = true;
            }
        }

        /// <inheritdoc/>
        public override PrefabBase GetPrefab() { return m_SelectedPrefab; }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestEnable() {
            m_Log.Debug($"RequestEnable()");

            if (m_ToolSystem.activeTool != this) {
                // Check for valid prefab selection before continuing.
                // ObjectToolSystem objectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
                // SelectedPrefab = objectToolSystem.prefab;
                m_ToolSystem.selected   = Entity.Null;
                m_ToolSystem.activeTool = this;
                UpdateSelectedPrefab();
            }
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void RequestDisable() {
            m_Log.Debug($"RequestDisable()");

            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void UpdateSelectedPrefab() {
            m_Log.Debug($"UpdateSelectedPrefab() -- {m_SelectedParcelSize.x}x{m_SelectedParcelSize.y}");

            // Todo abstract this
            var id = new PrefabID("ParcelPrefab", $"Parcel {m_SelectedParcelSize.x}x{m_SelectedParcelSize.y}");

            m_Log.Debug($"UpdateSelectedPrefab() -- Attempting to get Prefab with id {id}");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug($"UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }

            m_Log.Debug($"UpdateSelectedPrefab() -- Found ${prefabBase}!");

            var result = TrySetPrefab(prefabBase);

            m_Log.Debug($"UpdateSelectedPrefab() -- Result {result}");
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            // References & State
            Instance = this;
            Enabled  = false;

            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(P_RoadsideToolSystem));
            m_Log.Debug("OnCreate()");

            // Queries
            m_HighlightedQuery = SystemAPI.QueryBuilder().WithAll<Highlighted>().Build();
            m_DefinitionQuery  = GetDefinitionQuery();
            m_TempQuery        = SystemAPI.QueryBuilder().WithAll<Temp>().Build();

            // Get Systems
            m_RaycastSystem        = World.GetOrCreateSystemManaged<RaycastSystem>();
            m_CameraUpdateSystem   = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            m_PlatterOverlaySystem = World.GetOrCreateSystemManaged<P_OverlaySystem>();
            m_ObjectSearchSystem   = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_NetSearchSystem      = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ZoneSearchSystem     = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();

            // Data
            m_ControlPoints = new NativeList<ControlPoint>(1, Allocator.Persistent);
            m_UpgradeStates = new NativeList<NetToolSystem.UpgradeState>(10, Allocator.Persistent);
            m_Rotation      = new NativeReference<Rotation>(Allocator.Persistent);

            // Add tool to the tool system
            RegisterTool();

            // Set apply and cancel actions from game settings.
            m_ApplyAction  = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.ApplyActionName);
            m_CancelAction = PlatterMod.Instance.Settings.GetAction(PlatterModSettings.CancelActionName);
        }

        private void RegisterTool() {
            var toolList = World.GetOrCreateSystemManaged<ToolSystem>().tools;

            // Find the index of the "Object Tool" and remove any existing instance of this tool.
            var objectToolIndex = toolList.FindIndex(t => t.toolID == "Object Tool");
            toolList.Remove(this);

            // Insert this tool immediately before the Object Tool if found, otherwise insert at 0.
            var insertIndex = objectToolIndex > 0 ? objectToolIndex - 1 : 0;
            toolList.Insert(insertIndex, this);
        }

        /// <inheritdoc/>
        protected override void OnStartRunning() {
            base.OnStartRunning();
            m_Log.Debug("OnStartRunning()");

            // Ensure apply action is enabled.
            m_ApplyAction.shouldBeEnabled  = true;
            m_CancelAction.shouldBeEnabled = true;

            // Clear any previous raycast result.
            m_LastHitPosition = default;

            // Clear any applications.
            applyMode = ApplyMode.Clear;

            // Visualization
            base.requireZones = true;
            base.requireAreas = AreaTypeMask.Lots;
        }

        /// <inheritdoc/>
        protected override void OnStopRunning() {
            base.OnStopRunning();
            m_Log.Debug("OnStopRunning()");

            Reset();
        }

        /// <inheritdoc/>
        protected override void OnDestroy() {
            base.OnDestroy();
            m_ControlPoints.Dispose();
            m_UpgradeStates.Dispose();
            m_Log.Debug("OnDestroy()");
        }

        /// <inheritdoc/>
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab(prefab {prefab})");

            if (m_ToolSystem.activeTool != this || prefab is not ParcelPrefab parcelPrefab) {
                return false;
            }

            m_SelectedPrefab = parcelPrefab;
            return true;
        }

        protected override bool GetAllowApply() { return base.GetAllowApply() && !m_TempQuery.IsEmptyIgnoreFilter; }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            const string logPrefix = "OnUpdate()";

            var buffer = World.GetOrCreateSystemManaged<OverlayRenderSystem>().GetBuffer(out var bufferJobHandle);

            inputDeps = JobHandle.CombineDependencies(inputDeps, bufferJobHandle);

            // If this tool is not the active tool, clear UI state and bail out.
            if (m_ToolSystem.activeTool != this) {
                return Clear(inputDeps);
            }

            // ###
            // First, let's handle any "create" actions requested
            // ###
            if (ApplyWasRequested) {
                m_Log.Debug($"{logPrefix} -- Create action.");
                m_Log.Debug($"{logPrefix} -- ApplyWasRequested: {ApplyWasRequested}");

                // Reset
                ApplyWasRequested = false;

                // Validate
                if (m_RoadEditorData.SelectedEdgeEntity == Entity.Null || m_SelectedEntity == Entity.Null) {
                    m_Log.Debug($"{logPrefix} -- Create action invalid, aborting.");
                    m_Log.Debug($"{logPrefix} -- SelectedEdgeEntity: {m_RoadEditorData.SelectedEdgeEntity}");
                    m_Log.Debug($"{logPrefix} -- m_SelectedEntity: {m_SelectedEntity}");

                    return inputDeps;
                }

                // All Good!
                applyMode = ApplyMode.Apply;

                // Reset the tool
                Reset();
                return inputDeps;
            }

            // ###
            // This part of the code is about handling raycast hits and action presses.
            // It will update entity references and tool states.
            // ###
            if (GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                // Store results
                var previousHoveredEntity = m_HoveredEntity;
                m_HoveredEntity   = entity;
                m_LastHitPosition = raycastHit.m_HitPosition;

                // Check for apply action initiation.
                var applyWasPressed = applyAction.WasPressedThisFrame();

                if (applyWasPressed) {
                    if (m_RoadEditorData.Start(entity)) {
                        m_Log.Debug($"{logPrefix} -- Selected entity: {entity}");
                        m_RoadEditorData.RecalculatePoints = true;
                        SwapHighlitedEntities(m_RoadEditorData.SelectedEdgeEntity, entity);
                    }
                } else if (previousHoveredEntity != m_HoveredEntity) {
                    // If we were previously hovering over an Edge that isn't the selected one, unhighlight it.
                    if (previousHoveredEntity != m_RoadEditorData.SelectedEdgeEntity) {
                        ChangeHighlighting(previousHoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                    }

                    // Highlight the hovered entity
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.AddHighlight);
                }
            } else if (cancelAction.WasPressedThisFrame() ||
                       (m_RoadEditorData.SelectedEdgeEntity != Entity.Null &&
                        !EntityManager.Exists(m_RoadEditorData.SelectedEdgeEntity))) {
                m_Log.Debug($"{logPrefix} -- Cancel action.");

                // Right click & we had something selected -> deselect and reset the tool.
                Reset();
            } else if (m_HoveredEntity != Entity.Null) {
                // No raycast hit, no action pressed, remove hover from any entity that was being hovered before.
                if (m_HoveredEntity != m_RoadEditorData.SelectedEdgeEntity) {
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                }

                m_HoveredEntity   = Entity.Null;
                m_LastHitPosition = float3.zero;
            }

            // Exit early if we do not have a road selected
            if (m_RoadEditorData.SelectedEdgeEntity == Entity.Null) {
                return inputDeps;
            }

            // Exit early if we do not need to recalculate points at this stage.
            if (!m_RoadEditorData.RecalculatePoints) {
                applyMode = ApplyMode.None; // This will "keep" temp entities
                return inputDeps;
            }

            // ###
            // This part of the code handles the creation of entities.
            // ###
            var terrainHeightData = m_TerrainSystem.GetHeightData();
            m_Points.Clear();

            var rotation    = 1; // Demo data
            var parcelWidth = m_SelectedParcelSize.x * DimensionConstants.CellSize;

            m_Log.Debug("Calculating parcel points.");
            m_Log.Debug($"RoadEditorSpacing ${RoadEditorSpacing.ToJSONString()}");
            m_Log.Debug($"RoadEditorOffset ${RoadEditorOffset.ToJSONString()}");
            m_Log.Debug($"RoadEditorSides ${RoadEditorSides.ToJSONString()}");

            m_RoadEditorData.CalculatePoints(
                RoadEditorSpacing,
                rotation,
                parcelWidth / 2,
                parcelWidth / 2,
                RoadEditorOffset,
                parcelWidth,
                m_Points,
                ref terrainHeightData,
                RoadEditorSides,
                buffer,
                true
            );

            m_Log.Debug($"Rendering {m_Points.Count} parcels");

            // Step along length and place preview objects.
            foreach (var transformData in m_Points) {
                m_Log.Debug($"Creating temp parcel from enitity {m_SelectedEntity} transform {transformData.ToJSONString()}");
                MakeCreateDefinitionsJob(
                    m_SelectedEntity,
                    transformData.m_Position,
                    transformData.m_Rotation,
                    RandomSeed.Next()
                );
            }

            return inputDeps;
        }

        public void Reset() {
            m_Log.Debug("Reset() -- Tool reset requested.");
            ChangeHighlighting(m_RoadEditorData.SelectedEdgeEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
            m_HoveredEntity   = Entity.Null;
            m_LastHitPosition = float3.zero;
            m_Points.Clear();
            m_RoadEditorData.Reset();
        }

        public void RequestApply() { applyMode = ApplyMode.Apply; }

        internal void SwapHighlitedEntities(Entity oldEntity, Entity newEntity) {
            ChangeHighlighting(oldEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(newEntity, ChangeObjectHighlightMode.AddHighlight);
        }

        internal void ChangeHighlighting(Entity entity, ChangeObjectHighlightMode mode) {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) {
                return;
            }

            var wasChanged = false;

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

        private JobHandle HandleUpgradeMode(JobHandle inputDeps) {
            switch (m_State) {
                case State.Default when m_ApplyBlocked: {
                    if (applyAction.WasReleasedThisFrame() || secondaryApplyAction.WasReleasedThisFrame()) {
                        m_ApplyBlocked = false;
                    }

                    return Update(inputDeps, false);
                }
                case State.Default when applyAction.WasPressedThisFrame():
                    return Apply(inputDeps, applyAction.WasReleasedThisFrame());
                case State.Default when secondaryApplyAction.WasPressedThisFrame():
                    return Cancel(inputDeps, secondaryApplyAction.WasReleasedThisFrame());
                case State.Adding when cancelAction.WasPressedThisFrame():
                case State.Removing when cancelAction.WasPressedThisFrame():
                    m_ApplyBlocked = true;
                    m_State        = State.Default;
                    return Update(inputDeps, true);
                case State.Adding when applyAction.WasReleasedThisFrame():
                    return Apply(inputDeps);
                case State.Removing when secondaryApplyAction.WasReleasedThisFrame(): {
                    return Cancel(inputDeps);
                }
                case State.Default:
                case State.Adding:
                case State.Removing:
                case State.Rotating:
                default:
                    return Update(inputDeps, false);
            }
        }

        private JobHandle HandleToolInput(JobHandle inputDeps, bool forceCancel, Mode m_ToolMode) {
            // Cancel pressed (global)
            if (cancelAction.WasPressedThisFrame()) {
                if (m_ToolMode == Mode.Upgrade && (m_SnapOnMask & ~m_SnapOffMask & Snap.OwnerSide) != Snap.None) {
                    m_ToolSystem.activeTool = m_DefaultToolSystem;
                }

                return Cancel(inputDeps, cancelAction.WasReleasedThisFrame());
            }

            switch (m_State) {
                // Adding / Removing state handling (quick path)
                case State.Adding:
                case State.Removing: {
                    if (applyAction.WasPressedThisFrame() || applyAction.WasReleasedThisFrame()) {
                        return Apply(inputDeps);
                    }

                    if (forceCancel || secondaryApplyAction.WasPressedThisFrame() ||
                        secondaryApplyAction.WasReleasedThisFrame()) {
                        return Cancel(inputDeps);
                    }

                    return Update(inputDeps, false);
                }
                // Rotating (release)
                case State.Rotating when secondaryApplyAction.WasReleasedThisFrame(): {
                    if (m_RotationModified) {
                        m_RotationModified = false;
                    } else {
                        Rotate(0.7853982f, false, true);
                    }

                    m_State = State.Default;
                    return Update(inputDeps, false);
                }
                case State.Default:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Secondary apply (cancel) when allowed
            if ((m_ToolMode                                       != Mode.Upgrade ||
                 (m_SnapOnMask & ~m_SnapOffMask & Snap.OwnerSide) == Snap.None)
                && secondaryApplyAction.WasPressedThisFrame()) {
                return Cancel(inputDeps, secondaryApplyAction.WasReleasedThisFrame());
            }

            // Precise rotation
            if (m_PreciseRotation.IsInProgress()) {
                if (m_State != State.Default) {
                    return Update(inputDeps, true);
                }

                var delta = m_PreciseRotation.ReadValue<float>();
                var angle = 1.5707964f * delta * UnityEngine.Time.deltaTime;
                Rotate(angle, false, false);
                for (var j = 0; j < m_ControlPoints.Length; j++) {
                    var cp2 = m_ControlPoints[j];
                    cp2.m_Rotation     = m_Rotation.Value.m_Rotation;
                    m_ControlPoints[j] = cp2;
                }

                return Update(inputDeps, true);
            }

            // Rotation
            if (m_State                                   == State.Rotating &&
                InputManager.instance.activeControlScheme == InputManager.ControlScheme.KeyboardAndMouse) {
                float3 mousePos = InputManager.instance.mousePosition;
                if (mousePos.x != m_RotationStartPosition.x) {
                    var num3 = (mousePos.x - m_RotationStartPosition.x) * 6.2831855f * 0.002f;
                    Rotate(num3, true, false);
                    m_RotationModified = true;
                }

                return Update(inputDeps, false);
            }

            // Default update path
            return Update(inputDeps, false);
        }

        private void ResetTool() {
            if (m_ToolSystem.activeTool == this) {
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            }
        }

        private void Randomize() {
            m_RandomSeed = RandomSeed.Next();

            if (m_SelectedPrefab == null                                                                                ||
                !m_PrefabSystem.TryGetComponentData<PlaceableObjectData>(m_SelectedPrefab, out var placeableObjectData) ||
                placeableObjectData.m_RotationSymmetry == RotationSymmetry.None) {
                return;
            }

            var random = m_RandomSeed.GetRandom(567890109);
            var value  = m_Rotation.Value;
            var num    = 6.2831855f;

            if (placeableObjectData.m_RotationSymmetry == RotationSymmetry.Any) {
                num               = random.NextFloat(num);
                value.m_IsAligned = false;
            } else {
                num *= random.NextInt((int)placeableObjectData.m_RotationSymmetry) /
                       (float)placeableObjectData.m_RotationSymmetry;
            }

            value.m_Rotation = math.normalizesafe(math.mul(value.m_Rotation, quaternion.RotateY(num)), quaternion.identity);
            if (value.m_IsAligned) {
                //SnapJob.AlignRotation(ref value.m_Rotation, value.m_ParentRotation, false);
            }

            m_Rotation.Value = value;
        }

        private void Rotate(float angle, bool fromStart, bool align) {
            m_PrefabSystem.TryGetComponentData<PlaceableObjectData>(m_Prefab, out var placeableObjectData);
            var value = m_Rotation.Value;
            var flag  = (placeableObjectData.m_Flags & Game.Objects.PlacementFlags.Wall) > Game.Objects.PlacementFlags.None;
            value.m_Rotation = math.mul(
                fromStart ? m_StartRotation : value.m_Rotation,
                flag ? quaternion.RotateZ(angle) : quaternion.RotateY(angle));
            value.m_Rotation = math.normalizesafe(value.m_Rotation, quaternion.identity);
            if (align) {
                var quaternion = value.m_ParentRotation;
                //SnapJob.AlignRotation(ref value.m_Rotation, quaternion, flag);
            }

            value.m_IsAligned = align;
            m_Rotation.Value  = value;
        }

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            return inputDeps;
        }

        private JobHandle Cancel(JobHandle inputDeps, bool singleFrameOnly = false) {
            //if (m_ToolMode == Mode.Brush) {
            //if (m_State == State.Default) {
            //    base.applyMode = ApplyMode.Clear;
            //    Randomize();
            //    m_StartPoint = m_LastRaycastPoint;
            //    m_State = State.Removing;
            //    m_ForceCancel = singleFrameOnly;
            //    GetRaycastResult(out m_LastRaycastPoint);
            //    return UpdateDefinitions(inputDeps);
            //}
            //if (m_State == State.Removing && GetAllowApply()) {
            //    base.applyMode = ApplyMode.Apply;
            //    Randomize();
            //    m_StartPoint = default(ControlPoint);
            //    m_State = State.Default;
            //    GetRaycastResult(out m_LastRaycastPoint);
            //    return UpdateDefinitions(inputDeps);
            //}
            //base.applyMode = ApplyMode.Clear;
            //m_StartPoint = default(ControlPoint);
            //m_State = State.Default;
            //GetRaycastResult(out m_LastRaycastPoint);
            //return UpdateDefinitions(inputDeps);
            //} else {
            //if (m_State != State.Removing && m_UpgradeStates.Length >= 1) {
            //    m_State = State.Removing;
            //    m_ForceCancel = singleFrameOnly;
            //    m_ForceUpdate = true;
            //    m_AppliedUpgrade.Value = default(NetToolSystem.AppliedUpgrade);
            //    return Update(inputDeps, true);
            //}
            //if (m_State == State.Removing) {
            //    m_State = State.Default;
            //    if (GetAllowApply()) {
            //        SetAppliedUpgrade(true);
            //        base.applyMode = ApplyMode.Apply;
            //        m_RandomSeed = RandomSeed.Next();
            //        m_ControlPoints.Clear();
            //        m_UpgradeStates.Clear();
            //        ControlPoint controlPoint;
            //        if (GetRaycastResult(out controlPoint)) {
            //            m_ControlPoints.Add(in controlPoint);
            //            inputDeps = SnapControlPoint(inputDeps);
            //            inputDeps = FixNetControlPoints(inputDeps);
            //            inputDeps = UpdateDefinitions(inputDeps);
            //
            // m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PolygonToolRemovePointSound, 1f);
            //        } else {
            //            inputDeps = base.DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            //        }
            //    } else {
            //        base.applyMode = ApplyMode.Clear;
            //        m_ControlPoints.Clear();
            //        m_UpgradeStates.Clear();
            //        ControlPoint controlPoint2;
            //        if (GetRaycastResult(out controlPoint2)) {
            //            m_ControlPoints.Add(in controlPoint2);
            //            inputDeps = SnapControlPoint(inputDeps);
            //            inputDeps = UpdateDefinitions(inputDeps);
            //        } else {
            //            inputDeps = base.DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            //        }
            //    }
            //    return inputDeps;
            //}
            //if ((m_ToolMode != Mode.Upgrade || (m_SnapOnMask & ~m_SnapOffMask & Snap.OwnerSide) == Snap.None) &&
            // m_ControlPoints.Length <= 1) {
            //    if (singleFrameOnly) {
            //        Rotate(0.7853982f, false, true);
            //    } else {
            //        m_State = State.Rotating;
            //        m_RotationStartPosition = InputManager.instance.mousePosition;
            //        m_StartRotation = m_Rotation.Value.m_Rotation;
            //        m_StartCameraAngle = cameraAngle;
            //    }
            //}
            applyMode = ApplyMode.Clear;

            if (m_ControlPoints.Length > 0) {
                m_ControlPoints.RemoveAt(m_ControlPoints.Length - 1);
            }

            if (!GetRaycastResult(out var controlPoint)) {
                return inputDeps;
            }

            //controlPoint.m_Rotation = m_Rotation.Value.m_Rotation;
            if (m_ControlPoints.Length > 0) {
                m_ControlPoints[^1] = controlPoint;
            } else {
                m_ControlPoints.Add(in controlPoint);
            }

            inputDeps = SnapControlPoint(inputDeps);
            inputDeps = UpdateDefinitions(inputDeps);
            return inputDeps;
            //}
        }

        private JobHandle Apply(JobHandle inputDeps, bool singleFrameOnly = false) {
            if (m_State != State.Adding && m_UpgradeStates.Length >= 1 && !singleFrameOnly) {
                m_State                = State.Adding;
                m_ForceUpdate          = true;
                m_AppliedUpgrade.Value = default;
                return Update(inputDeps, true);
            }

            //if (m_State == State.Adding) {
            //    m_State = State.Default;
            //    if (GetAllowApply()) {
            //        SetAppliedUpgrade(false);
            //        applyMode    = ApplyMode.Apply;
            //        m_RandomSeed = RandomSeed.Next();
            //        m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_NetBuildSound, 1f);
            //        m_ControlPoints.Clear();
            //        m_UpgradeStates.Clear();
            //        ControlPoint controlPoint;
            //        if (GetRaycastResult(out controlPoint)) {
            //            m_ControlPoints.Add(in controlPoint);
            //            inputDeps = SnapControlPoint(inputDeps);
            //            inputDeps = FixNetControlPoints(inputDeps);
            //            inputDeps = UpdateDefinitions(inputDeps);
            //        } else {
            //            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            //        }
            //    } else {
            //        m_ControlPoints.Clear();
            //        m_UpgradeStates.Clear();
            //        m_ForceUpdate = true;
            //        inputDeps     = Update(inputDeps, true);
            //    }

            //    return inputDeps;
            //}

            //if (m_ControlPoints.Length < GetMaxControlPointCount()) {
            //    applyMode = ApplyMode.Clear;
            //    ControlPoint controlPoint2;
            //    if (GetRaycastResult(out controlPoint2)) {
            //        if (m_ControlPoints.Length <= 1) {
            //            m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_NetStartSound, 1f);
            //        } else {
            //            m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_NetNodeSound, 1f);
            //        }

            //        controlPoint2.m_Rotation = m_Rotation.Value.m_Rotation;
            //        m_ControlPoints.Add(in controlPoint2);
            //        inputDeps = SnapControlPoint(inputDeps);
            //        inputDeps = UpdateDefinitions(inputDeps);
            //    } else {
            //        inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            //    }
            //} else if (GetAllowApply()) {
            //    applyMode = ApplyMode.Apply;
            //    Randomize();
            //    if (m_Prefab is BuildingPrefab || m_Prefab is AssetStampPrefab) {
            //        m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlaceBuildingSound, 1f);
            //    } else if (m_Prefab is StaticObjectPrefab || m_ToolSystem.actionMode.IsEditor()) {
            //        m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlacePropSound, 1f);
            //    }

            //    m_ControlPoints.Clear();
            //    m_UpgradeStates.Clear();
            //    m_AppliedUpgrade.Value = default;
            //    if (m_ToolSystem.actionMode.IsGame() && !m_LotQuery.IsEmptyIgnoreFilter) {
            //        using (NativeArray<Entity> nativeArray = m_LotQuery.ToEntityArray(Allocator.TempJob)) {
            //            for (var i = 0; i < nativeArray.Length; i++) {
            //                var     entity         = nativeArray[i];
            //                ref var componentData  = EntityManager.GetComponentData<Area>(entity);
            //                var     componentData2 = EntityManager.GetComponentData<Temp>(entity);
            //                if ((componentData.m_Flags  & AreaFlags.Slave)  == 0 &&
            //                    (componentData2.m_Flags & TempFlags.Create) != 0U) {
            //                    var prefab = m_PrefabSystem.GetPrefab<LotPrefab>(
            //                        EntityManager.GetComponentData<PrefabRef>(entity));
            //                    if (!prefab.m_AllowOverlap) {
            //                        m_AreaToolSystem.recreate = entity;
            //                        m_AreaToolSystem.prefab   = prefab;
            //                        m_AreaToolSystem.mode     = AreaToolSystem.Mode.Edit;
            //                        m_ToolSystem.activeTool   = m_AreaToolSystem;
            //                        return inputDeps;
            //                    }
            //                }
            //            }
            //        }
            //    }

            //    ControlPoint controlPoint3;
            //    if (GetRaycastResult(out controlPoint3)) {
            //        if (m_ToolSystem.actionMode.IsGame()) {
            //            Telemetry.PlaceBuilding(m_UpgradingObject, m_Prefab, controlPoint3.m_Position);
            //        }

            //        controlPoint3.m_Rotation = m_Rotation.Value.m_Rotation;
            //        m_ControlPoints.Add(in controlPoint3);
            //        inputDeps = SnapControlPoint(inputDeps);
            //        inputDeps = UpdateDefinitions(inputDeps);
            //    }
            //} else {
            //    m_AudioManager.PlayUISound(m_SoundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlaceBuildingFailSound, 1f);
            //    inputDeps = Update(inputDeps, false);
            //}

            return inputDeps;
        }

        private JobHandle Update(JobHandle inputDeps, bool fullUpdate) {
            ControlPoint controlPoint;
            bool         forceUpdate;
            if (GetRaycastResult(out controlPoint, out forceUpdate)) {
                controlPoint.m_Rotation = m_Rotation.Value.m_Rotation;
                forceUpdate             = forceUpdate || fullUpdate;
                applyMode               = ApplyMode.None;
                if (!m_LastRaycastPoint.Equals(controlPoint) || forceUpdate) {
                    m_LastRaycastPoint = controlPoint;
                    var controlPoint2 = default(ControlPoint);
                    if (m_ControlPoints.Length != 0) {
                        controlPoint2 = m_ControlPoints[m_ControlPoints.Length - 1];
                    }

                    if (m_State == State.Adding || m_State == State.Removing) {
                        if (m_ControlPoints.Length == 1) {
                            m_ControlPoints.Add(in controlPoint);
                        } else {
                            m_ControlPoints[m_ControlPoints.Length - 1] = controlPoint;
                        }
                    } else {
                        if (m_UpgradeStates.Length != 0) {
                            m_ControlPoints.Clear();
                            m_UpgradeStates.Clear();
                        }

                        if (m_ControlPoints.Length == 0) {
                            m_ControlPoints.Add(in controlPoint);
                        } else {
                            m_ControlPoints[m_ControlPoints.Length - 1] = controlPoint;
                        }
                    }

                    inputDeps = SnapControlPoint(inputDeps);
                    JobHandle.ScheduleBatchedJobs();
                    if (!forceUpdate) {
                        inputDeps.Complete();
                        var controlPoint3 = m_ControlPoints[m_ControlPoints.Length - 1];
                        forceUpdate = !controlPoint2.EqualsIgnoreHit(controlPoint3);
                    }

                    if (forceUpdate) {
                        applyMode = ApplyMode.Clear;
                        inputDeps = UpdateDefinitions(inputDeps);
                    }
                }
            } else {
                applyMode          = ApplyMode.Clear;
                m_LastRaycastPoint = default;
                if (m_State == State.Default) {
                    m_ControlPoints.Clear();
                    m_UpgradeStates.Clear();
                    m_AppliedUpgrade.Value = default;
                }

                inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            }

            return inputDeps;
        }

        private JobHandle SnapControlPoint(JobHandle inputDeps) { return inputDeps; }

        private JobHandle UpdateDefinitions(JobHandle inputDeps) {
            //var jobHandle = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            //if (m_Prefab != null) {
            //    var actualSnap = GetActualSnap();
            //    var entity     = m_PrefabSystem.GetEntity(m_Prefab);
            //if (m_ToolMode != Mode.Brush && (actualSnap & Snap.NetArea) != Snap.None) {
            //    if (m_State == State.Adding || m_State == State.Removing) {
            //        return JobHandle.CombineDependencies(jobHandle, UpdateSubReplacementDefinitions(inputDeps));
            //    }

            //    PlaceableObjectData placeableObjectData;
            //    if (EntityManager.TryGetComponent(entity, out placeableObjectData) &&
            //        placeableObjectData.m_SubReplacementType != SubReplacementType.None) {
            //        inputDeps.Complete();
            //        if (m_UpgradeStates.Length != 0) {
            //            return JobHandle.CombineDependencies(jobHandle, UpdateSubReplacementDefinitions(default(JobHandle)));
            //        }
            //    }
            //}

            //    var @null   = Entity.Null;
            //    var entity2 = Entity.Null;
            //    var entity3 = Entity.Null;
            //    var num     = UnityEngine.Time.deltaTime;
            //    var num2    = 0f;
            //    if (m_ToolSystem.actionMode.IsEditor()) {
            //        Entity entity4;
            //        GetContainers(m_ContainerQuery, out @null, out entity4);
            //    }

            //    if (m_TransformPrefab != null) {
            //        entity2 = m_PrefabSystem.GetEntity(m_TransformPrefab);
            //    }

            //    if (m_ToolMode == Mode.Brush && brushType != null) {
            //        entity3 = m_PrefabSystem.GetEntity(brushType);
            //        EnsureCachedBrushData();
            //        var startPoint       = m_StartPoint;
            //        var lastRaycastPoint = m_LastRaycastPoint;
            //        startPoint.m_OriginalEntity       = Entity.Null;
            //        lastRaycastPoint.m_OriginalEntity = Entity.Null;
            //        m_ControlPoints.Clear();
            //        m_UpgradeStates.Clear();
            //        m_AppliedUpgrade.Value = default;
            //        m_ControlPoints.Add(in startPoint);
            //        m_ControlPoints.Add(in lastRaycastPoint);
            //        if (m_State == State.Default) {
            //            num = 0.1f;
            //        }
            //    }

            //    if (m_ToolMode == Mode.Line || m_ToolMode == Mode.Curve) {
            //        num2 = math.max(1f, distance) * distanceScale;
            //    }

            //    var                     nativeReference = default(NativeReference<AttachmentData>);
            //    PlaceholderBuildingData placeholderBuildingData;
            //    if (!m_ToolSystem.actionMode.IsEditor() && EntityManager.TryGetComponent(entity, out placeholderBuildingData)) {
            //        var componentData  = EntityManager.GetComponentData<ZoneData>(placeholderBuildingData.m_ZonePrefab);
            //        var componentData2 = EntityManager.GetComponentData<BuildingData>(entity);
            //        m_BuildingQuery.ResetFilter();
            //        m_BuildingQuery.SetSharedComponentFilter<BuildingSpawnGroupData>(
            //            new BuildingSpawnGroupData(componentData.m_ZoneType));
            //        nativeReference = new NativeReference<AttachmentData>(Allocator.TempJob);
            //        JobHandle jobHandle2;
            //        NativeList<ArchetypeChunk> nativeList =
            //        m_BuildingQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out jobHandle2);
            //        var findAttachmentBuildingJob = default(ObjectToolSystem.FindAttachmentBuildingJob);
            //        findAttachmentBuildingJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(
            //            ref __TypeHandle.__Unity_Entities_Entity_TypeHandle, CheckedStateRef);
            //        findAttachmentBuildingJob.m_BuildingDataType =
            // InternalCompilerInterface.GetComponentTypeHandle<BuildingData>(
            //            ref __TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentTypeHandle, CheckedStateRef);
            //        findAttachmentBuildingJob.m_SpawnableBuildingType =
            //        InternalCompilerInterface.GetComponentTypeHandle<SpawnableBuildingData>(
            //            ref __TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentTypeHandle, CheckedStateRef);
            //        findAttachmentBuildingJob.m_BuildingData = componentData2;
            //        findAttachmentBuildingJob.m_RandomSeed = m_RandomSeed;
            //        findAttachmentBuildingJob.m_Chunks = nativeList;
            //        findAttachmentBuildingJob.m_AttachmentPrefab = nativeReference;
            //        inputDeps = findAttachmentBuildingJob.Schedule(JobHandle.CombineDependencies(inputDeps, jobHandle2));
            //        nativeList.Dispose(inputDeps);
            //    }

            //    jobHandle = JobHandle.CombineDependencies(
            //        jobHandle,
            //        CreateDefinitions(
            //            entity, entity2, entity3, m_UpgradingObject, m_MovingObject, @null,
            //            m_CityConfigurationSystem.defaultTheme, m_ControlPoints, nativeReference,
            //            m_ToolSystem.actionMode.IsEditor(), m_CityConfigurationSystem.leftHandTraffic, m_State ==
            // State.Removing,
            //            m_ToolMode == Mode.Stamp, brushSize, math.radians(brushAngle), brushStrength, num2, num, m_RandomSeed,
            //            actualSnap, actualAgeMask, inputDeps));
            //    if (nativeReference.IsCreated) {
            //        nativeReference.Dispose(jobHandle);
            //    }
            //}

            return inputDeps;
        }

        /// <inheritdoc/>
        public override void InitializeRaycast() {
            base.InitializeRaycast();

            if (m_SelectedPrefab == null) {
                return;
            }

            GetAvailableSnapMask(out var onMask, out var offMask);
            var actualSnap = GetActualSnap(selectedSnap, onMask, offMask);

            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround;
            m_ToolRaycastSystem.typeMask      = TypeMask.Lanes | TypeMask.Net;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements |
                                               RaycastFlags.Cargo   | RaycastFlags.Passenger;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
        }

        private static int GetMaxControlPointCount() { return 1; }

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }

        private struct Rotation {
            public quaternion m_Rotation;

            public quaternion m_ParentRotation;

            public bool m_IsAligned;

            public bool m_IsSnapped;
        }

        public abstract class ToolData {
            protected EntityManager m_EntityManager;
            protected PrefabSystem m_PrefabSystem;

            public ToolData(EntityManager entityManager, PrefabSystem prefabSystem) {
                m_EntityManager = entityManager;
                m_PrefabSystem = prefabSystem;
            }
        }

        public class ToolData_RoadEditor : ToolData {
            public Entity SelectedEdgeEntity { get; set; }

            public Curve SelectedCurve { get; set; }

            public NetCompositionData SelectedCurveGeo { get; set; }

            public EdgeGeometry SelectedCurveEdgeGeo { get; set; }

            public StartNodeGeometry SelectedCurveStartGeo { get; set; }

            public EndNodeGeometry SelectedCurveEndGeo { get; set; }

            public PrefabBase SelectedPrefabBase { get; set; }

            public bool RecalculatePoints { get; set; }

            public ToolData_RoadEditor(EntityManager entityManager, PrefabSystem prefabSystem)
            : base(entityManager, prefabSystem) { }

            /// <inheritdoc/>
            public bool Start(Entity entity) {
                if (!(
                        m_EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef) &&
                        m_EntityManager.TryGetComponent<Curve>(entity, out var curve) &&
                        m_EntityManager.TryGetComponent<EdgeGeometry>(entity, out var edgeGeo) &&
                        m_EntityManager.TryGetComponent<StartNodeGeometry>(entity, out var startGeo) &&
                        m_EntityManager.TryGetComponent<EndNodeGeometry>(entity, out var endGeo) &&
                        m_EntityManager.TryGetComponent<Composition>(entity, out var composition) &&
                        m_EntityManager.TryGetComponent<NetCompositionData>(
                            composition.m_Edge,
                            out var edgeNetCompData) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase))
                   ) {
                    return false;
                }

                SelectedEdgeEntity = entity;
                SelectedCurve = curve;
                SelectedCurveGeo = edgeNetCompData;
                SelectedCurveEdgeGeo = edgeGeo;
                SelectedCurveStartGeo = startGeo;
                SelectedCurveEndGeo = endGeo;
                SelectedPrefabBase = prefabBase;
                return true;
            }

            /// <inheritdoc/>
            public void Reset() {
                SelectedEdgeEntity = Entity.Null;
                SelectedCurve = default;
                SelectedCurveGeo = default;
                SelectedCurveEdgeGeo = default;
                SelectedPrefabBase = null;
                RecalculatePoints = false;
            }

            /// <summary>
            /// Calculates the points to use based on this mode.
            /// </summary>
            /// <param name="cintPositionOnCurvePos">Selection current position.</param>
            /// <param name="spacingMode">Active spacing mode.</param>
            /// <param name="rotationMode">Active rotation mode.</param>
            /// <param name="spacing">Spacing distance.</param>
            /// <param name="randomSpacing">Random spacing offset maximum.</param>
            /// <param name="randomOffset">Random lateral offset maximum.</param>
            /// <param name="rotation">Rotation setting.</param>
            /// <param name="zBounds">Prefab zBounds.</param>
            /// <param name="pointList">List of points to populate.</param>
            /// <param name="heightData">Terrain height data reference.</param>
            public virtual void CalculatePoints(float spacing,
                                                int rotation,
                                                float startOffset,
                                                float endOffset,
                                                float roadOffset,
                                                float parcelWidth,
                                                List<Transform> pointList,
                                                ref TerrainHeightData heightData,
                                                bool4 sides,
                                                OverlayRenderSystem.Buffer overlayBuffer,
                                                bool debug) {
                RecalculatePoints = false;

                // Make sure there's always a little offset, for directional vector calc
                roadOffset += 0.1f;

                // Don't do anything if we don't have a valid start point.
                if (SelectedEdgeEntity == Entity.Null) {
                    return;
                }

                // Curves
                var curvesDict = new Dictionary<string, List<CurveData>> {
                { "left", new List<CurveData>() },
                { "right", new List<CurveData>() },
                { "start", new List<CurveData>() },
                { "end", new List<CurveData>() },
            };

                // The specific order in which these curves are added to each list is important,
                // as we're trying to create a continuous curve to calculate points on
                if (sides.x) {
                    curvesDict["left"].Add(new CurveData(SelectedCurveEdgeGeo.m_Start.m_Left, -1f));
                    curvesDict["left"].Add(new CurveData(SelectedCurveEdgeGeo.m_End.m_Left, -1f));
                }

                if (sides.y) {
                    curvesDict["right"].Add(new CurveData(SelectedCurveEdgeGeo.m_Start.m_Right, 1f));
                    curvesDict["right"].Add(new CurveData(SelectedCurveEdgeGeo.m_End.m_Right, 1f));
                }

                if (sides.z) {
                    curvesDict["start"].Add(new CurveData(SelectedCurveStartGeo.m_Geometry.m_Left.m_Right, 1f));
                    curvesDict["start"].Add(new CurveData(SelectedCurveStartGeo.m_Geometry.m_Right.m_Right, 1f));
                    curvesDict["start"].Add(
                        new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveStartGeo.m_Geometry.m_Right.m_Left), 1f));
                    curvesDict["start"].Add(
                        new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveStartGeo.m_Geometry.m_Left.m_Left), 1f));
                }

                if (sides.w) {
                    curvesDict["end"].Add(new CurveData(SelectedCurveEndGeo.m_Geometry.m_Left.m_Left, -1f));
                    curvesDict["end"].Add(new CurveData(SelectedCurveEndGeo.m_Geometry.m_Right.m_Left, -1f));
                    curvesDict["end"].Add(
                        new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveEndGeo.m_Geometry.m_Right.m_Right), -1f));
                    curvesDict["end"].Add(
                        new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveEndGeo.m_Geometry.m_Left.m_Right), -1f));
                }

                foreach (var curves in curvesDict.Values.Where(curves => curves.Count != 0)) {
                    // Draw debug circles if needed
                    if (debug) {
                        var debugLinesColor = 0;
                        foreach (var curve in curves) {
                            overlayBuffer.DrawCircle(
                                ColorConstants.DebugColors[debugLinesColor], new Color(1f, 1f, 1f, 1f), 0.2f,
                                OverlayRenderSystem.StyleFlags.Grid,
                                new float2(1, 1), curve.StartPointLocation, 3f);
                            overlayBuffer.DrawCircle(
                                ColorConstants.DebugColors[debugLinesColor], new Color(0f, 0f, 0f, 1f), 0.2f,
                                OverlayRenderSystem.StyleFlags.Grid,
                                new float2(1, 1), curve.EndPointLocation, 3f);
                            overlayBuffer.DrawCurve(ColorConstants.DebugColors[debugLinesColor], curve.Curve, 1f);
                            debugLinesColor = debugLinesColor < ColorConstants.DebugColors.Length - 1 ? debugLinesColor + 1 : 0;
                        }
                    }

                    var totalLength = 0f;

                    // Calculate total length of curve and set the relative starting position of each individual curve
                    for (var i = 0; i < curves.Count; i++) {
                        var curve = curves[i];
                        if (curves.ElementAtOrDefault(i - 1) != null) {
                            curve.RelativeStartingPosition = curves[i - 1].RelativeStartingPosition + curves[i - 1].Length;
                        }

                        totalLength += curve.Length;
                    }

                    // Reduce length by start and end offsets
                    totalLength = totalLength - startOffset - endOffset;

                    var pointsCount = math.floor((totalLength + spacing) / (parcelWidth + spacing));
                    var intervals = pointsCount - 1;
                    var stepLength = intervals == 0 ? 0 : totalLength / intervals;

                    var currentCurveIndex = 0;
                    var debugPointsColor = 0;
                    var currentPosition = startOffset;

                    // Generate points, retrieving the correct position on the correct curve.
                    for (var i = 0; i < pointsCount; i++) {
                        // Get the right curve given the currentPosition
                        if (currentPosition > curves[currentCurveIndex].RelativeStartingPosition + curves[currentCurveIndex].Length &&
                            currentCurveIndex < curves.Count - 1) {
                            currentCurveIndex++;
                        }

                        var currentCurve = curves[currentCurveIndex];
                        var relativePosition = currentPosition - currentCurve.RelativeStartingPosition;
                        var relativePercentage = relativePosition / currentCurve.Length;
                        var pointPositionOnCurve = MathUtils.Position(
                            currentCurve.Curve,
                            relativePercentage
                        );
                        var tangent = MathUtils.Tangent(currentCurve.Curve, relativePercentage);
                        var perpendicular2dVector = new Vector3(tangent.z, 0f, -tangent.x);
                        float3 normal = perpendicular2dVector.normalized;
                        var shiftedPointPosition = pointPositionOnCurve + normal * roadOffset * currentCurve.Direction;
                        Vector3 direction = pointPositionOnCurve - shiftedPointPosition;
                        var perpendicularRotation = Quaternion.LookRotation(direction.normalized);

                        if (debug) {
                            overlayBuffer.DrawCircle(ColorConstants.DebugColors[debugPointsColor], shiftedPointPosition, 3f);
                            debugPointsColor = debugPointsColor < ColorConstants.DebugColors.Length - 1 ? debugPointsColor + 1 : 0;
                        }

                        pointList.Add(
                            new Transform {
                                m_Position = shiftedPointPosition,
                                m_Rotation = perpendicularRotation,
                            });

                        // Increase pointer
                        currentPosition += stepLength;
                    }
                }
            }
        }

        /// <summary>
        /// Creates temporary object definitions for previewing.
        /// </summary>
        /// <param name="objectPrefab">Object prefab entity.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="rotation">Entity rotation.</param>
        /// <param name="randomSeed">Random seed to use.</param>
        private void MakeCreateDefinitionsJob(Entity objectPrefab, float3 position, quaternion rotation, RandomSeed randomSeed) {
            var createDefJob = new CreateDefinitions {
                m_EditorMode = m_ToolSystem.actionMode.IsEditor(),
                m_LefthandTraffic = m_CityConfigurationSystem.leftHandTraffic,
                m_ObjectPrefab = objectPrefab,
                m_Theme = m_CityConfigurationSystem.defaultTheme,
                m_RandomSeed = randomSeed,
                m_ControlPoint = new ControlPoint {
                    m_Position = position,
                    m_Rotation = rotation,
                },
                m_AttachmentPrefab = default,
                m_OwnerData = SystemAPI.GetComponentLookup<Owner>(true),
                m_TransformData = SystemAPI.GetComponentLookup<Transform>(true),
                m_AttachedData = SystemAPI.GetComponentLookup<Attached>(true),
                m_LocalTransformCacheData = SystemAPI.GetComponentLookup<LocalTransformCache>(true),
                m_ElevationData = SystemAPI.GetComponentLookup<Game.Objects.Elevation>(true),
                m_BuildingData = SystemAPI.GetComponentLookup<Building>(true),
                m_LotData = SystemAPI.GetComponentLookup<Game.Buildings.Lot>(true),
                m_EdgeData = SystemAPI.GetComponentLookup<Game.Net.Edge>(true),
                m_NodeData = SystemAPI.GetComponentLookup<Game.Net.Node>(true),
                m_CurveData = SystemAPI.GetComponentLookup<Curve>(true),
                m_NetElevationData = SystemAPI.GetComponentLookup<Game.Net.Elevation>(true),
                m_OrphanData = SystemAPI.GetComponentLookup<Orphan>(true),
                m_UpgradedData = SystemAPI.GetComponentLookup<Upgraded>(true),
                m_CompositionData = SystemAPI.GetComponentLookup<Composition>(true),
                m_AreaClearData = SystemAPI.GetComponentLookup<Clear>(true),
                m_AreaSpaceData = SystemAPI.GetComponentLookup<Space>(true),
                m_AreaLotData = SystemAPI.GetComponentLookup<Game.Areas.Lot>(true),
                m_EditorContainerData = SystemAPI.GetComponentLookup<Game.Tools.EditorContainer>(true),
                m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_PrefabNetObjectData = SystemAPI.GetComponentLookup<NetObjectData>(true),
                m_PrefabBuildingData = SystemAPI.GetComponentLookup<BuildingData>(true),
                m_PrefabAssetStampData = SystemAPI.GetComponentLookup<AssetStampData>(true),
                m_PrefabBuildingExtensionData =
                SystemAPI.GetComponentLookup<BuildingExtensionData>(true),
                m_PrefabSpawnableObjectData =
                SystemAPI.GetComponentLookup<SpawnableObjectData>(true),
                m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                m_PrefabPlaceableObjectData =
                SystemAPI.GetComponentLookup<PlaceableObjectData>(true),
                m_PrefabAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>(true),
                m_PrefabBuildingTerraformData =
                SystemAPI.GetComponentLookup<BuildingTerraformData>(true),
                m_PrefabCreatureSpawnData = SystemAPI.GetComponentLookup<CreatureSpawnData>(true),
                m_PlaceholderBuildingData =
                SystemAPI.GetComponentLookup<PlaceholderBuildingData>(true),
                m_PrefabNetGeometryData = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                m_PrefabCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>(true),
                m_SubObjects = SystemAPI.GetBufferLookup<Game.Objects.SubObject>(true),
                m_CachedNodes = SystemAPI.GetBufferLookup<LocalNodeCache>(true),
                m_InstalledUpgrades = SystemAPI.GetBufferLookup<InstalledUpgrade>(true),
                m_SubNets = SystemAPI.GetBufferLookup<Game.Net.SubNet>(true),
                m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                m_SubAreas = SystemAPI.GetBufferLookup<Game.Areas.SubArea>(true),
                m_AreaNodes = SystemAPI.GetBufferLookup<Game.Areas.Node>(true),
                m_AreaTriangles = SystemAPI.GetBufferLookup<Triangle>(true),
                m_PrefabSubObjects = SystemAPI.GetBufferLookup<Game.Prefabs.SubObject>(true),
                m_PrefabSubNets = SystemAPI.GetBufferLookup<Game.Prefabs.SubNet>(true),
                m_PrefabSubLanes = SystemAPI.GetBufferLookup<Game.Prefabs.SubLane>(true),
                m_PrefabSubAreas = SystemAPI.GetBufferLookup<Game.Prefabs.SubArea>(true),
                m_PrefabSubAreaNodes = SystemAPI.GetBufferLookup<SubAreaNode>(true),
                m_PrefabPlaceholderElements =
                SystemAPI.GetBufferLookup<PlaceholderObjectElement>(true),
                m_PrefabRequirementElements =
                SystemAPI.GetBufferLookup<ObjectRequirementElement>(true),
                m_PrefabServiceUpgradeBuilding =
                SystemAPI.GetBufferLookup<ServiceUpgradeBuilding>(true),
                m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out _),
                m_TerrainHeightData = m_TerrainSystem.GetHeightData(),
                m_CommandBuffer = m_ToolOutputBarrier.CreateCommandBuffer(),
            };

            createDefJob.Execute();
        }

        public class CurveData {
            public Bezier4x3 Curve;
            public float Direction = 1;
            public float3 EndPointLocation;
            public float Length;
            public float RelativeStartingPosition;
            public float3 StartPointLocation;

            public CurveData(Bezier4x3 curve, float direction) {
                Curve = curve;
                Length = MathUtils.Length(curve);
                Direction = direction;
                StartPointLocation = MathUtils.Position(
                    Curve,
                    0f
                );
                EndPointLocation = MathUtils.Position(
                    Curve,
                    1f
                );
            }
        }

        internal struct CreateDefinitions : IJob {
            [ReadOnly]
            public bool m_EditorMode;

            [ReadOnly]
            public bool m_LefthandTraffic;

            [ReadOnly]
            public Entity m_ObjectPrefab;

            [ReadOnly]
            public Entity m_Theme;

            [ReadOnly]
            public RandomSeed m_RandomSeed;

            [ReadOnly]
            public ControlPoint m_ControlPoint;

            [ReadOnly]
            public NativeReference<AttachmentData> m_AttachmentPrefab;

            [ReadOnly]
            public ComponentLookup<Owner> m_OwnerData;

            [ReadOnly]
            public ComponentLookup<Transform> m_TransformData;

            [ReadOnly]
            public ComponentLookup<Attached> m_AttachedData;

            [ReadOnly]
            public ComponentLookup<LocalTransformCache> m_LocalTransformCacheData;

            [ReadOnly]
            public ComponentLookup<Game.Objects.Elevation> m_ElevationData;

            [ReadOnly]
            public ComponentLookup<Building> m_BuildingData;

            [ReadOnly]
            public ComponentLookup<Game.Buildings.Lot> m_LotData;

            [ReadOnly]
            public ComponentLookup<Game.Net.Edge> m_EdgeData;

            [ReadOnly]
            public ComponentLookup<Game.Net.Node> m_NodeData;

            [ReadOnly]
            public ComponentLookup<Curve> m_CurveData;

            [ReadOnly]
            public ComponentLookup<Game.Net.Elevation> m_NetElevationData;

            [ReadOnly]
            public ComponentLookup<Orphan> m_OrphanData;

            [ReadOnly]
            public ComponentLookup<Upgraded> m_UpgradedData;

            [ReadOnly]
            public ComponentLookup<Clear> m_AreaClearData;

            [ReadOnly]
            public ComponentLookup<Composition> m_CompositionData;

            [ReadOnly]
            public ComponentLookup<Space> m_AreaSpaceData;

            [ReadOnly]
            public ComponentLookup<Game.Areas.Lot> m_AreaLotData;

            [ReadOnly]
            public ComponentLookup<Game.Tools.EditorContainer> m_EditorContainerData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<NetObjectData> m_PrefabNetObjectData;

            [ReadOnly]
            public ComponentLookup<BuildingData> m_PrefabBuildingData;

            [ReadOnly]
            public ComponentLookup<AssetStampData> m_PrefabAssetStampData;

            [ReadOnly]
            public ComponentLookup<BuildingExtensionData> m_PrefabBuildingExtensionData;

            [ReadOnly]
            public ComponentLookup<SpawnableObjectData> m_PrefabSpawnableObjectData;

            [ReadOnly]
            public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

            [ReadOnly]
            public ComponentLookup<PlaceableObjectData> m_PrefabPlaceableObjectData;

            [ReadOnly]
            public ComponentLookup<AreaGeometryData> m_PrefabAreaGeometryData;

            [ReadOnly]
            public ComponentLookup<PlaceholderBuildingData> m_PlaceholderBuildingData;

            [ReadOnly]
            public ComponentLookup<BuildingTerraformData> m_PrefabBuildingTerraformData;

            [ReadOnly]
            public ComponentLookup<CreatureSpawnData> m_PrefabCreatureSpawnData;

            [ReadOnly]
            public BufferLookup<Game.Objects.SubObject> m_SubObjects;

            [ReadOnly]
            public ComponentLookup<NetGeometryData> m_PrefabNetGeometryData;

            [ReadOnly]
            public ComponentLookup<NetCompositionData> m_PrefabCompositionData;

            [ReadOnly]
            public BufferLookup<LocalNodeCache> m_CachedNodes;

            [ReadOnly]
            public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;

            [ReadOnly]
            public BufferLookup<Game.Net.SubNet> m_SubNets;

            [ReadOnly]
            public BufferLookup<ConnectedEdge> m_ConnectedEdges;

            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> m_SubAreas;

            [ReadOnly]
            public BufferLookup<Game.Areas.Node> m_AreaNodes;

            [ReadOnly]
            public BufferLookup<Triangle> m_AreaTriangles;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubObject> m_PrefabSubObjects;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubNet> m_PrefabSubNets;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubLane> m_PrefabSubLanes;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubArea> m_PrefabSubAreas;

            [ReadOnly]
            public BufferLookup<SubAreaNode> m_PrefabSubAreaNodes;

            [ReadOnly]
            public BufferLookup<PlaceholderObjectElement> m_PrefabPlaceholderElements;

            [ReadOnly]
            public BufferLookup<ObjectRequirementElement> m_PrefabRequirementElements;

            [ReadOnly]
            public BufferLookup<ServiceUpgradeBuilding> m_PrefabServiceUpgradeBuilding;

            [ReadOnly]
            public WaterSurfaceData m_WaterSurfaceData;

            [ReadOnly]
            public TerrainHeightData m_TerrainHeightData;

            public EntityCommandBuffer m_CommandBuffer;

            public void                Execute() { }


            //public void Execute() {
                //    var controlPoint = m_ControlPoint;
                //    var entity = Entity.Null;
                //    var controlPointEntity = Entity.Null;
                //    var updatedTopLevel = Entity.Null;
                //    var lotEntity = Entity.Null;
                //    OwnerDefinition ownerDefinition = default;
                //    var upgrade = false;
                //    var flag = controlPointEntity != Entity.Null;
                //    var topLevel = true;
                //    var parentMesh = !(entity != Entity.Null) ? -1 : 0;

                //    if (!flag && m_PrefabNetObjectData.HasComponent(m_ObjectPrefab) &&
                //        m_AttachedData.HasComponent(controlPoint.m_OriginalEntity) &&
                //        (m_EditorMode || !m_OwnerData.HasComponent(controlPoint.m_OriginalEntity))) {
                //        var attached = m_AttachedData[controlPoint.m_OriginalEntity];
                //        if (m_NodeData.HasComponent(attached.m_Parent) || m_EdgeData.HasComponent(attached.m_Parent)) {
                //            controlPointEntity = controlPoint.m_OriginalEntity;
                //            controlPoint.m_OriginalEntity = attached.m_Parent;
                //            upgrade = true;
                //        }
                //    }

                //    if (m_EditorMode) {
                //        var entity3 = controlPoint.m_OriginalEntity;
                //        var num = controlPoint.m_ElementIndex.x;
                //        while (m_OwnerData.HasComponent(entity3) && !m_BuildingData.HasComponent(entity3)) {
                //            if (m_LocalTransformCacheData.HasComponent(entity3)) {
                //                num = m_LocalTransformCacheData[entity3].m_ParentMesh;
                //                num += math.select(1000, -1000, num < 0);
                //            }

                //            entity3 = m_OwnerData[entity3].m_Owner;
                //        }

                //        if (m_InstalledUpgrades.TryGetBuffer(entity3, out var bufferData) && bufferData.Length != 0) {
                //            entity3 = bufferData[0].m_Upgrade;
                //        }

                //        var flag2 = false;
                //        if (m_PrefabRefData.TryGetComponent(entity3, out var componentData) &&
                //            m_PrefabServiceUpgradeBuilding.TryGetBuffer(m_ObjectPrefab, out var bufferData2)) {
                //            var entity4 = Entity.Null;
                //            if (m_TransformData.TryGetComponent(entity3, out var componentData2) &&
                //                m_PrefabBuildingExtensionData.TryGetComponent(m_ObjectPrefab, out var componentData3)) {
                //                for (var i = 0; i < bufferData2.Length; i++)
                //                    if (bufferData2[i].m_Building == componentData.m_Prefab) {
                //                        entity4 = entity3;
                //                        controlPoint.m_Position = ObjectUtils.LocalToWorld(componentData2, componentData3.m_Position);
                //                        controlPoint.m_Rotation = componentData2.m_Rotation;
                //                        break;
                //                    }
                //            }

                //            entity3 = entity4;
                //            flag2 = true;
                //        }

                //        if (m_TransformData.HasComponent(entity3) && m_SubObjects.HasBuffer(entity3)) {
                //            entity = entity3;
                //            topLevel = flag2;
                //            parentMesh = num;
                //        }

                //        if (m_OwnerData.HasComponent(controlPointEntity)) {
                //            var owner = m_OwnerData[controlPointEntity];
                //            if (owner.m_Owner != entity) {
                //                entity = owner.m_Owner;
                //                topLevel = flag2;
                //                parentMesh = -1;
                //            }
                //        }

                //        if (!m_EdgeData.HasComponent(controlPoint.m_OriginalEntity) &&
                //            !m_NodeData.HasComponent(controlPoint.m_OriginalEntity)) {
                //            controlPoint.m_OriginalEntity = Entity.Null;
                //        }
                //    }

                //    NativeList<ClearAreaData> clearAreas = default;
                //    if (m_TransformData.HasComponent(entity)) {
                //        var transform = m_TransformData[entity];
                //        m_ElevationData.TryGetComponent(entity, out var componentData5);
                //        var owner = Entity.Null;
                //        if (m_OwnerData.HasComponent(entity)) {
                //            owner = m_OwnerData[entity].m_Owner;
                //        }

                //        ownerDefinition.m_Prefab = m_PrefabRefData[entity].m_Prefab;
                //        ownerDefinition.m_Position = transform.m_Position;
                //        ownerDefinition.m_Rotation = transform.m_Rotation;

                //        if (CheckParentPrefab(ownerDefinition.m_Prefab, m_ObjectPrefab)) {
                //            updatedTopLevel = entity;
                //            if (m_PrefabServiceUpgradeBuilding.HasBuffer(m_ObjectPrefab)) {
                //                ClearAreaHelpers.FillClearAreas(
                //                    ownerTransform: new Transform(controlPoint.m_Position, controlPoint.m_Rotation),
                //                    ownerPrefab: m_ObjectPrefab, prefabObjectGeometryData: m_PrefabObjectGeometryData,
                //                    prefabAreaGeometryData: m_PrefabAreaGeometryData, prefabSubAreas: m_PrefabSubAreas,
                //                    prefabSubAreaNodes: m_PrefabSubAreaNodes, clearAreas: ref clearAreas);
                //                ClearAreaHelpers.InitClearAreas(clearAreas, transform);
                //                if (controlPointEntity == Entity.Null) {
                //                    lotEntity = entity;
                //                }
                //            }

                //            var flag3 = m_ObjectPrefab == Entity.Null;
                //            var parent = Entity.Null;
                //            if (flag3 && m_InstalledUpgrades.TryGetBuffer(entity, out var bufferData3)) {
                //                ClearAreaHelpers.FillClearAreas(
                //                    bufferData3, Entity.Null, m_TransformData, m_AreaClearData, m_PrefabRefData,
                //                    m_PrefabObjectGeometryData, m_SubAreas, m_AreaNodes, m_AreaTriangles, ref clearAreas);
                //                ClearAreaHelpers.InitClearAreas(clearAreas, transform);
                //            }

                //            if (flag3 && m_AttachedData.TryGetComponent(entity, out var componentData6) &&
                //                m_BuildingData.HasComponent(componentData6.m_Parent)) {
                //                var transform2 = m_TransformData[componentData6.m_Parent];
                //                parent = m_PrefabRefData[componentData6.m_Parent].m_Prefab;
                //                PlatterMod.Instance.Log.Debug("UpdateObject(), 1");
                //                UpdateObject(
                //                    Entity.Null, Entity.Null, componentData6.m_Parent, Entity.Null, componentData6.m_Parent,
                //                    Entity.Null, transform2, 0f, default, clearAreas, false, false, false,
                //                    true, false, -1, -1);
                //            }

                //            PlatterMod.Instance.Log.Debug("UpdateObject(), 2");
                //            UpdateObject(
                //                Entity.Null, owner, entity, parent, updatedTopLevel, Entity.Null, transform,
                //                componentData5.m_Elevation, default, clearAreas, true, false, flag3,
                //                true, false, -1, -1);
                //            if (clearAreas.IsCreated) {
                //                clearAreas.Clear();
                //            }
                //        } else {
                //            ownerDefinition = default;
                //        }
                //    }

                //    if (controlPointEntity != Entity.Null &&
                //        m_InstalledUpgrades.TryGetBuffer(controlPointEntity, out var bufferData4)) {
                //        ClearAreaHelpers.FillClearAreas(
                //            bufferData4, Entity.Null, m_TransformData, m_AreaClearData, m_PrefabRefData, m_PrefabObjectGeometryData,
                //            m_SubAreas, m_AreaNodes, m_AreaTriangles, ref clearAreas);
                //        ClearAreaHelpers.TransformClearAreas(
                //            clearAreas, m_TransformData[controlPointEntity],
                //            new Transform(controlPoint.m_Position, controlPoint.m_Rotation));
                //        ClearAreaHelpers.InitClearAreas(clearAreas, new Transform(controlPoint.m_Position, controlPoint.m_Rotation));
                //    }

                //    if (m_ObjectPrefab != Entity.Null) {
                //        // if (controlPointEntity == Entity.Null && ownerDefinition.m_Prefab == Entity.Null &&
                //        // m_PrefabPlaceholderElements.TryGetBuffer(m_ObjectPrefab, out var bufferData5) &&
                //        // !m_PrefabCreatureSpawnData.HasComponent(m_ObjectPrefab)) {
                //        //    var random = m_RandomSeed.GetRandom(1000000);
                //        //    var num2 = 0;
                //        //    for (var j = 0; j < bufferData5.Length; j++) {
                //        //        if (GetVariationData(bufferData5[j], out var variation)) {
                //        //            num2 += variation.m_Probability;
                //        //            if (random.NextInt(num2) < variation.m_Probability) {
                //        //                entity5 = variation.m_Prefab;
                //        //            }
                //        //        }
                //        //    }
                //        // }

                //        // THIS ONE!
                //        PlatterMod.Instance.Log.Debug("UpdateObject(), 3");
                //        UpdateObject(
                //            m_ObjectPrefab,
                //            Entity.Null,
                //            controlPointEntity,
                //            controlPoint.m_OriginalEntity,
                //            updatedTopLevel,
                //            lotEntity,
                //            new Transform(
                //                controlPoint.m_Position,
                //                controlPoint.m_Rotation
                //            ),
                //            controlPoint.m_Elevation,
                //            ownerDefinition,
                //            clearAreas,
                //            upgrade,
                //            flag,
                //            false,
                //            topLevel,
                //            false,
                //            parentMesh,
                //            0);

                //        if (m_AttachmentPrefab.IsCreated && m_AttachmentPrefab.Value.m_Entity != Entity.Null) {
                //            var transform3 = new Transform(controlPoint.m_Position, controlPoint.m_Rotation);
                //            transform3.m_Position += math.rotate(transform3.m_Rotation, m_AttachmentPrefab.Value.m_Offset);
                //            PlatterMod.Instance.Log.Debug("UpdateObject(), 4");
                //            UpdateObject(
                //                m_AttachmentPrefab.Value.m_Entity, Entity.Null, Entity.Null, m_ObjectPrefab, updatedTopLevel,
                //                Entity.Null, transform3, controlPoint.m_Elevation, ownerDefinition, clearAreas, false,
                //                false, false, topLevel, false, parentMesh, 0);
                //        }
                //    }

                //    if (clearAreas.IsCreated) {
                //        clearAreas.Dispose();
                //    }
                //}

                //private bool CheckParentPrefab(Entity parentPrefab, Entity objectPrefab) {
                //    if (parentPrefab == objectPrefab) {
                //        return false;
                //    }

                //    if (m_PrefabSubObjects.HasBuffer(objectPrefab)) {
                //        var dynamicBuffer = m_PrefabSubObjects[objectPrefab];
                //        for (var i = 0; i < dynamicBuffer.Length; i++)
                //            if (!CheckParentPrefab(parentPrefab, dynamicBuffer[i].m_Prefab)) {
                //                return false;
                //            }
                //    }

                //    return true;
                //}

                //private void UpdateObject(Entity objectPrefab,
                //                          Entity owner,
                //                          Entity original,
                //                          Entity parent,
                //                          Entity updatedTopLevel,
                //                          Entity lotEntity,
                //                          Transform transform,
                //                          float elevation,
                //                          OwnerDefinition ownerDefinition,
                //                          NativeList<ClearAreaData> clearAreas,
                //                          bool upgrade,
                //                          bool relocate,
                //                          bool rebuild,
                //                          bool topLevel,
                //                          bool optional,
                //                          int parentMesh,
                //                          int randomIndex) {
                //    var newOwnerDef = ownerDefinition;
                //    var random = m_RandomSeed.GetRandom(randomIndex);

                //    if (!m_PrefabAssetStampData.HasComponent(objectPrefab) || ownerDefinition.m_Prefab == Entity.Null) {
                //        var newEntity = m_CommandBuffer.CreateEntity();

                //        CreationDefinition creationDef = default;
                //        creationDef.m_Prefab = objectPrefab;
                //        creationDef.m_SubPrefab = Entity.Null;
                //        creationDef.m_Owner = owner;
                //        creationDef.m_Original = original;

                //        // Set random seed.
                //        creationDef.m_RandomSeed = random.NextInt();

                //        if (optional) {
                //            creationDef.m_Flags |= CreationFlags.Optional;
                //        }

                //        // Original = control point entity, doesnt apply
                //        if (objectPrefab == Entity.Null && m_PrefabRefData.HasComponent(original)) {
                //            objectPrefab = m_PrefabRefData[original].m_Prefab;
                //        }

                //        if (m_PrefabBuildingData.HasComponent(objectPrefab)) {
                //            parentMesh = -1;
                //        }

                //        ObjectDefinition objectDef = default;
                //        objectDef.m_ParentMesh = parentMesh;
                //        objectDef.m_Position = transform.m_Position;
                //        objectDef.m_Rotation = transform.m_Rotation;
                //        objectDef.m_Probability = 100;
                //        objectDef.m_PrefabSubIndex = -1;
                //        objectDef.m_Scale = 1f;
                //        objectDef.m_Intensity = 1f;

                //        if (m_PrefabPlaceableObjectData.HasComponent(objectPrefab)) {
                //            var placeableObjectData = m_PrefabPlaceableObjectData[objectPrefab];
                //            if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.HasProbability) != 0) {
                //                objectDef.m_Probability = placeableObjectData.m_DefaultProbability;
                //            }
                //        }

                //        if (m_EditorContainerData.HasComponent(original)) {
                //            var editorContainer = m_EditorContainerData[original];
                //            creationDef.m_SubPrefab = editorContainer.m_Prefab;
                //            objectDef.m_Scale = editorContainer.m_Scale;
                //            objectDef.m_Intensity = editorContainer.m_Intensity;
                //            objectDef.m_GroupIndex = editorContainer.m_GroupIndex;
                //        }

                //        if (m_LocalTransformCacheData.HasComponent(original)) {
                //            var localTransformCache = m_LocalTransformCacheData[original];
                //            objectDef.m_Probability = localTransformCache.m_Probability;
                //            objectDef.m_PrefabSubIndex = localTransformCache.m_PrefabSubIndex;
                //        }

                //        objectDef.m_Elevation = parentMesh != -1 ? transform.m_Position.y - ownerDefinition.m_Position.y : elevation;

                //        if (ownerDefinition.m_Prefab != Entity.Null) {
                //            m_CommandBuffer.AddComponent(newEntity, ownerDefinition);
                //            var transform2 = ObjectUtils.WorldToLocal(
                //                ObjectUtils.InverseTransform(new Transform(ownerDefinition.m_Position, ownerDefinition.m_Rotation)),
                //                transform);
                //            objectDef.m_LocalPosition = transform2.m_Position;
                //            objectDef.m_LocalRotation = transform2.m_Rotation;
                //        } else if (m_TransformData.HasComponent(owner)) {
                //            var transform3 = ObjectUtils.WorldToLocal(
                //                ObjectUtils.InverseTransform(m_TransformData[owner]), transform);
                //            objectDef.m_LocalPosition = transform3.m_Position;
                //            objectDef.m_LocalRotation = transform3.m_Rotation;
                //        } else {
                //            objectDef.m_LocalPosition = transform.m_Position;
                //            objectDef.m_LocalRotation = transform.m_Rotation;
                //        }

                //        var entity = Entity.Null;
                //        if (m_SubObjects.HasBuffer(parent)) {
                //            creationDef.m_Flags |= CreationFlags.Attach;
                //            if (parentMesh == -1 && m_NetElevationData.HasComponent(parent)) {
                //                objectDef.m_ParentMesh = 0;
                //                objectDef.m_Elevation = math.csum(m_NetElevationData[parent].m_Elevation) * 0.5f;
                //                if (IsLoweredParent(parent)) {
                //                    creationDef.m_Flags |= CreationFlags.Lowered;
                //                }
                //            }

                //            if (m_PrefabNetObjectData.HasComponent(objectPrefab)) {
                //                entity = parent;
                //                UpdateAttachedParent(parent, updatedTopLevel);
                //            } else {
                //                creationDef.m_Attached = parent;
                //            }
                //        } else if (m_PlaceholderBuildingData.HasComponent(parent)) {
                //            creationDef.m_Flags |= CreationFlags.Attach;
                //            creationDef.m_Attached = parent;
                //        }

                //        if (m_AttachedData.HasComponent(original)) {
                //            var attached = m_AttachedData[original];
                //            if (attached.m_Parent != entity) {
                //                UpdateAttachedParent(attached.m_Parent, updatedTopLevel);
                //            }
                //        }

                //        if (upgrade) {
                //            creationDef.m_Flags |= CreationFlags.Upgrade | CreationFlags.Parent;
                //        }

                //        if (relocate) {
                //            creationDef.m_Flags |= CreationFlags.Relocate;
                //        }

                //        newOwnerDef.m_Prefab = objectPrefab;
                //        newOwnerDef.m_Position = objectDef.m_Position;
                //        newOwnerDef.m_Rotation = objectDef.m_Rotation;
                //        m_CommandBuffer.AddComponent(newEntity, creationDef);
                //        m_CommandBuffer.AddComponent(newEntity, objectDef);
                //        m_CommandBuffer.AddComponent(newEntity, default(Updated));
                //    } else {
                //        if (m_PrefabSubObjects.HasBuffer(objectPrefab)) {
                //            var dynamicBuffer = m_PrefabSubObjects[objectPrefab];
                //            for (var i = 0; i < dynamicBuffer.Length; i++) {
                //                var subObject = dynamicBuffer[i];
                //                var transform4 = ObjectUtils.LocalToWorld(transform, subObject.m_Position, subObject.m_Rotation);
                //                PlatterMod.Instance.Log.Debug("UpdateObject(), 0");
                //                UpdateObject(
                //                    subObject.m_Prefab, owner, Entity.Null, parent, updatedTopLevel, lotEntity, transform4, elevation,
                //                    ownerDefinition, default, false, false, false, false,
                //                    false, parentMesh, i);
                //            }
                //        }

                //        original = Entity.Null;
                //        topLevel = true;
                //    }

                //    NativeParallelHashMap<Entity, int> selectedSpawnables = default;
                //    var mainInverseTransform = transform;
                //    if (original != Entity.Null) {
                //        mainInverseTransform = ObjectUtils.InverseTransform(m_TransformData[original]);
                //    }

                //    UpdateSubObjects(
                //        transform, transform, mainInverseTransform, objectPrefab, original, relocate, rebuild, topLevel, upgrade,
                //        newOwnerDef, ref random, ref selectedSpawnables);
                //    UpdateSubNets(
                //        transform, transform, mainInverseTransform, objectPrefab, original, lotEntity, relocate, topLevel,
                //        newOwnerDef, clearAreas, ref random);
                //    UpdateSubAreas(
                //        transform, objectPrefab, original, relocate, rebuild, topLevel, newOwnerDef, clearAreas, ref random,
                //        ref selectedSpawnables);
                //    if (selectedSpawnables.IsCreated) {
                //        selectedSpawnables.Dispose();
                //    }
                //}

                //private void UpdateAttachedParent(Entity parent, Entity updatedTopLevel) {
                //    if (updatedTopLevel != Entity.Null) {
                //        var entity = parent;
                //        if (entity == updatedTopLevel) {
                //            return;
                //        }

                //        while (m_OwnerData.HasComponent(entity)) {
                //            entity = m_OwnerData[entity].m_Owner;
                //            if (entity == updatedTopLevel) {
                //                return;
                //            }
                //        }
                //    }

                //    if (m_EdgeData.HasComponent(parent)) {
                //        var edge = m_EdgeData[parent];
                //        var e = m_CommandBuffer.CreateEntity();
                //        CreationDefinition component = default;
                //        component.m_Original = parent;
                //        component.m_Flags |= CreationFlags.Align;
                //        m_CommandBuffer.AddComponent(e, component);
                //        m_CommandBuffer.AddComponent(e, default(Updated));
                //        NetCourse component2 = default;
                //        component2.m_Curve = m_CurveData[parent].m_Bezier;
                //        component2.m_Length = MathUtils.Length(component2.m_Curve);
                //        component2.m_FixedIndex = -1;
                //        component2.m_StartPosition.m_Entity = edge.m_Start;
                //        component2.m_StartPosition.m_Position = component2.m_Curve.a;
                //        component2.m_StartPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(component2.m_Curve));
                //        component2.m_StartPosition.m_CourseDelta = 0f;
                //        component2.m_EndPosition.m_Entity = edge.m_End;
                //        component2.m_EndPosition.m_Position = component2.m_Curve.d;
                //        component2.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(component2.m_Curve));
                //        component2.m_EndPosition.m_CourseDelta = 1f;
                //        m_CommandBuffer.AddComponent(e, component2);
                //    } else if (m_NodeData.HasComponent(parent)) {
                //        var node = m_NodeData[parent];
                //        var e2 = m_CommandBuffer.CreateEntity();
                //        CreationDefinition component3 = default;
                //        component3.m_Original = parent;
                //        m_CommandBuffer.AddComponent(e2, component3);
                //        m_CommandBuffer.AddComponent(e2, default(Updated));
                //        NetCourse component4 = default;
                //        component4.m_Curve = new Bezier4x3(node.m_Position, node.m_Position, node.m_Position, node.m_Position);
                //        component4.m_Length = 0f;
                //        component4.m_FixedIndex = -1;
                //        component4.m_StartPosition.m_Entity = parent;
                //        component4.m_StartPosition.m_Position = node.m_Position;
                //        component4.m_StartPosition.m_Rotation = node.m_Rotation;
                //        component4.m_StartPosition.m_CourseDelta = 0f;
                //        component4.m_EndPosition.m_Entity = parent;
                //        component4.m_EndPosition.m_Position = node.m_Position;
                //        component4.m_EndPosition.m_Rotation = node.m_Rotation;
                //        component4.m_EndPosition.m_CourseDelta = 1f;
                //        m_CommandBuffer.AddComponent(e2, component4);
                //    }
                //}

                //private bool IsLoweredParent(Entity entity) {
                //    if (m_CompositionData.TryGetComponent(entity, out var componentData) &&
                //        m_PrefabCompositionData.TryGetComponent(componentData.m_Edge, out var componentData2) &&
                //        ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0) {
                //        return true;
                //    }

                //    if (m_OrphanData.TryGetComponent(entity, out var componentData3) &&
                //        m_PrefabCompositionData.TryGetComponent(componentData3.m_Composition, out componentData2) &&
                //        ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0) {
                //        return true;
                //    }

                //    if (m_ConnectedEdges.TryGetBuffer(entity, out var bufferData)) {
                //        for (var i = 0; i < bufferData.Length; i++) {
                //            var connectedEdge = bufferData[i];
                //            var edge = m_EdgeData[connectedEdge.m_Edge];
                //            if (edge.m_Start == entity) {
                //                if (m_CompositionData.TryGetComponent(connectedEdge.m_Edge, out componentData) &&
                //                    m_PrefabCompositionData.TryGetComponent(componentData.m_StartNode, out componentData2) &&
                //                    ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) &
                //                     CompositionFlags.Side.Lowered) != 0) {
                //                    return true;
                //                }
                //            } else if (edge.m_End == entity &&
                //                       m_CompositionData.TryGetComponent(connectedEdge.m_Edge, out componentData) &&
                //                       m_PrefabCompositionData.TryGetComponent(componentData.m_EndNode, out componentData2) &&
                //                       ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) &
                //                        CompositionFlags.Side.Lowered) != 0) {
                //                return true;
                //            }
                //        }
                //    }

                //    return false;
                //}

                //private void UpdateSubObjects(Transform transform, Transform mainTransform, Transform mainInverseTransform,
                //                              Entity prefab, Entity original, bool relocate, bool rebuild, bool topLevel,
                //                              bool isParent, OwnerDefinition ownerDefinition, ref Random random,
                //                              ref NativeParallelHashMap<Entity, int> selectedSpawnables) {
                //    if (!m_InstalledUpgrades.HasBuffer(original) || !m_TransformData.HasComponent(original)) {
                //        return;
                //    }

                //    var inverseParentTransform = ObjectUtils.InverseTransform(m_TransformData[original]);
                //    var dynamicBuffer = m_InstalledUpgrades[original];
                //    Transform transform2 = default;
                //    for (var i = 0; i < dynamicBuffer.Length; i++) {
                //        var upgrade = dynamicBuffer[i].m_Upgrade;
                //        if (!m_TransformData.HasComponent(upgrade)) {
                //            continue;
                //        }

                //        var e = m_CommandBuffer.CreateEntity();
                //        CreationDefinition component = default;
                //        component.m_Original = upgrade;
                //        if (relocate) {
                //            component.m_Flags |= CreationFlags.Relocate;
                //        }

                //        if (isParent) {
                //            component.m_Flags |= CreationFlags.Parent;
                //            if (m_ObjectPrefab == Entity.Null) {
                //                component.m_Flags |= CreationFlags.Upgrade;
                //            }
                //        }

                //        m_CommandBuffer.AddComponent(e, component);
                //        m_CommandBuffer.AddComponent(e, default(Updated));
                //        if (ownerDefinition.m_Prefab != Entity.Null) {
                //            m_CommandBuffer.AddComponent(e, ownerDefinition);
                //        }

                //        ObjectDefinition component2 = default;
                //        component2.m_Probability = 100;
                //        component2.m_PrefabSubIndex = -1;
                //        if (m_LocalTransformCacheData.HasComponent(upgrade)) {
                //            var localTransformCache = m_LocalTransformCacheData[upgrade];
                //            component2.m_ParentMesh = localTransformCache.m_ParentMesh;
                //            component2.m_GroupIndex = localTransformCache.m_GroupIndex;
                //            component2.m_Probability = localTransformCache.m_Probability;
                //            component2.m_PrefabSubIndex = localTransformCache.m_PrefabSubIndex;
                //            transform2.m_Position = localTransformCache.m_Position;
                //            transform2.m_Rotation = localTransformCache.m_Rotation;
                //        } else {
                //            component2.m_ParentMesh = m_BuildingData.HasComponent(upgrade) ? -1 : 0;
                //            transform2 = ObjectUtils.WorldToLocal(inverseParentTransform, m_TransformData[upgrade]);
                //        }

                //        if (m_ElevationData.TryGetComponent(upgrade, out var componentData)) {
                //            component2.m_Elevation = componentData.m_Elevation;
                //        }

                //        var transform3 = ObjectUtils.LocalToWorld(transform, transform2);
                //        transform3.m_Rotation = math.normalize(transform3.m_Rotation);
                //        if (relocate && m_BuildingData.HasComponent(upgrade) &&
                //            m_PrefabRefData.TryGetComponent(upgrade, out var componentData2) &&
                //            m_PrefabPlaceableObjectData.TryGetComponent(componentData2.m_Prefab, out var componentData3)) {
                //            var num = TerrainUtils.SampleHeight(ref m_TerrainHeightData, transform3.m_Position);
                //            if ((componentData3.m_Flags & Game.Objects.PlacementFlags.Hovering) != 0) {
                //                var num2 = WaterUtils.SampleHeight(
                //                    ref m_WaterSurfaceData, ref m_TerrainHeightData, transform3.m_Position);
                //                num2 += componentData3.m_PlacementOffset.y;
                //                component2.m_Elevation = math.max(0f, num2 - num);
                //                num = math.max(num, num2);
                //            } else if ((componentData3.m_Flags &
                //                        (Game.Objects.PlacementFlags.Shoreline | Game.Objects.PlacementFlags.Floating)) == 0) {
                //                num += componentData3.m_PlacementOffset.y;
                //            } else {
                //                var num3 = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, transform3.m_Position, out float waterDepth);
                //                if (waterDepth >= 0.2f) {
                //                    num3 += componentData3.m_PlacementOffset.y;
                //                    if ((componentData3.m_Flags & Game.Objects.PlacementFlags.Floating) != 0) {
                //                        component2.m_Elevation = math.max(0f, num3 - num);
                //                    }

                //                    num = math.max(num, num3);
                //                }
                //            }

                //            transform3.m_Position.y = num;
                //        }

                //        component2.m_Position = transform3.m_Position;
                //        component2.m_Rotation = transform3.m_Rotation;
                //        component2.m_LocalPosition = transform2.m_Position;
                //        component2.m_LocalRotation = transform2.m_Rotation;
                //        m_CommandBuffer.AddComponent(e, component2);
                //        OwnerDefinition ownerDefinition2 = default;
                //        ownerDefinition2.m_Prefab = m_PrefabRefData[upgrade].m_Prefab;
                //        ownerDefinition2.m_Position = transform3.m_Position;
                //        ownerDefinition2.m_Rotation = transform3.m_Rotation;
                //        var ownerDefinition3 = ownerDefinition2;
                //        UpdateSubNets(
                //            transform3, mainTransform, mainInverseTransform, ownerDefinition3.m_Prefab, upgrade, Entity.Null,
                //            relocate, true, ownerDefinition3, default, ref random);
                //        UpdateSubAreas(
                //            transform3, ownerDefinition3.m_Prefab, upgrade, relocate, rebuild, true, ownerDefinition3,
                //            default, ref random, ref selectedSpawnables);
                //    }
                //}

                //private void CreateSubNet(Entity netPrefab, Entity lanePrefab, Bezier4x3 curve, int2 nodeIndex, int2 parentMesh,
                //                          CompositionFlags upgrades, NativeList<float4> nodePositions, Transform parentTransform,
                //                          OwnerDefinition ownerDefinition, NativeList<ClearAreaData> clearAreas,
                //                          BuildingUtils.LotInfo lotInfo, bool hasLot, ref Random random) {
                //    m_PrefabNetGeometryData.TryGetComponent(netPrefab, out var componentData);
                //    CreationDefinition component = default;
                //    component.m_Prefab = netPrefab;
                //    component.m_SubPrefab = lanePrefab;
                //    component.m_RandomSeed = random.NextInt();
                //    var flag = parentMesh.x >= 0 && parentMesh.y >= 0;
                //    NetCourse component2 = default;
                //    if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0) {
                //        curve.y = default;
                //        Curve curve2 = default;
                //        curve2.m_Bezier = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
                //        var curve3 = curve2;
                //        component2.m_Curve = NetUtils.AdjustPosition(
                //            curve3, false, false, false, ref m_TerrainHeightData,
                //            ref m_WaterSurfaceData).m_Bezier;
                //    } else if (!flag) {
                //        Curve curve2 = default;
                //        curve2.m_Bezier = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
                //        var curve4 = curve2;
                //        var flag2 = parentMesh.x >= 0;
                //        var flag3 = parentMesh.y >= 0;
                //        flag = flag2 || flag3;
                //        if ((componentData.m_Flags & Game.Net.GeometryFlags.FlattenTerrain) != 0) {
                //            if (hasLot) {
                //                component2.m_Curve = NetUtils.AdjustPosition(curve4, flag2, flag, flag3, ref lotInfo).m_Bezier;
                //                component2.m_Curve.a.y += curve.a.y;
                //                component2.m_Curve.b.y += curve.b.y;
                //                component2.m_Curve.c.y += curve.c.y;
                //                component2.m_Curve.d.y += curve.d.y;
                //            } else {
                //                component2.m_Curve = curve4.m_Bezier;
                //            }
                //        } else {
                //            component2.m_Curve = NetUtils.AdjustPosition(curve4, flag2, flag, flag3, ref m_TerrainHeightData)
                //                                         .m_Bezier;
                //            component2.m_Curve.a.y += curve.a.y;
                //            component2.m_Curve.b.y += curve.b.y;
                //            component2.m_Curve.c.y += curve.c.y;
                //            component2.m_Curve.d.y += curve.d.y;
                //        }
                //    } else {
                //        component2.m_Curve = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
                //    }

                //    var onGround = !flag || math.cmin(math.abs(curve.y.abcd)) < 2f;
                //    if (ClearAreaHelpers.ShouldClear(clearAreas, component2.m_Curve, onGround)) {
                //        return;
                //    }

                //    var e = m_CommandBuffer.CreateEntity();
                //    m_CommandBuffer.AddComponent(e, component);
                //    m_CommandBuffer.AddComponent(e, default(Updated));
                //    if (ownerDefinition.m_Prefab != Entity.Null) {
                //        m_CommandBuffer.AddComponent(e, ownerDefinition);
                //    }

                //    component2.m_StartPosition.m_Position = component2.m_Curve.a;
                //    component2.m_StartPosition.m_Rotation = NetUtils.GetNodeRotation(
                //        MathUtils.StartTangent(component2.m_Curve), parentTransform.m_Rotation);
                //    component2.m_StartPosition.m_CourseDelta = 0f;
                //    component2.m_StartPosition.m_Elevation = curve.a.y;
                //    component2.m_StartPosition.m_ParentMesh = parentMesh.x;
                //    if (nodeIndex.x >= 0) {
                //        if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0) {
                //            component2.m_StartPosition.m_Position.xz = ObjectUtils.LocalToWorld(
                //                parentTransform, nodePositions[nodeIndex.x].xyz).xz;
                //        } else {
                //            component2.m_StartPosition.m_Position = ObjectUtils.LocalToWorld(
                //                parentTransform, nodePositions[nodeIndex.x].xyz);
                //        }
                //    }

                //    component2.m_EndPosition.m_Position = component2.m_Curve.d;
                //    component2.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(
                //        MathUtils.EndTangent(component2.m_Curve), parentTransform.m_Rotation);
                //    component2.m_EndPosition.m_CourseDelta = 1f;
                //    component2.m_EndPosition.m_Elevation = curve.d.y;
                //    component2.m_EndPosition.m_ParentMesh = parentMesh.y;
                //    if (nodeIndex.y >= 0) {
                //        if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0) {
                //            component2.m_EndPosition.m_Position.xz = ObjectUtils.LocalToWorld(
                //                parentTransform, nodePositions[nodeIndex.y].xyz).xz;
                //        } else {
                //            component2.m_EndPosition.m_Position = ObjectUtils.LocalToWorld(
                //                parentTransform, nodePositions[nodeIndex.y].xyz);
                //        }
                //    }

                //    component2.m_Length = MathUtils.Length(component2.m_Curve);
                //    component2.m_FixedIndex = -1;
                //    component2.m_StartPosition.m_Flags |= CoursePosFlags.IsFirst;
                //    component2.m_EndPosition.m_Flags |= CoursePosFlags.IsLast;
                //    if (component2.m_StartPosition.m_Position.Equals(component2.m_EndPosition.m_Position)) {
                //        component2.m_StartPosition.m_Flags |= CoursePosFlags.IsLast;
                //        component2.m_EndPosition.m_Flags |= CoursePosFlags.IsFirst;
                //    }

                //    if (ownerDefinition.m_Prefab == Entity.Null) {
                //        component2.m_StartPosition.m_Flags |= CoursePosFlags.FreeHeight;
                //        component2.m_EndPosition.m_Flags |= CoursePosFlags.FreeHeight;
                //    }

                //    m_CommandBuffer.AddComponent(e, component2);
                //    if (upgrades != default) {
                //        Upgraded upgraded = default;
                //        upgraded.m_Flags = upgrades;
                //        var component3 = upgraded;
                //        m_CommandBuffer.AddComponent(e, component3);
                //    }

                //    if (m_EditorMode) {
                //        LocalCurveCache component4 = default;
                //        component4.m_Curve = curve;
                //        m_CommandBuffer.AddComponent(e, component4);
                //    }
                //}

                //private bool GetOwnerLot(Entity lotOwner, out BuildingUtils.LotInfo lotInfo) {
                //    if (m_LotData.TryGetComponent(lotOwner, out var componentData) &&
                //        m_TransformData.TryGetComponent(lotOwner, out var componentData2) &&
                //        m_PrefabRefData.TryGetComponent(lotOwner, out var componentData3) &&
                //        m_PrefabBuildingData.TryGetComponent(componentData3.m_Prefab, out var componentData4)) {
                //        var extents = new float2(componentData4.m_LotSize) * 4f;
                //        m_ElevationData.TryGetComponent(lotOwner, out var componentData5);
                //        m_InstalledUpgrades.TryGetBuffer(lotOwner, out var bufferData);
                //        lotInfo = BuildingUtils.CalculateLotInfo(
                //            extents, componentData2, componentData5, componentData, componentData3, bufferData, m_TransformData,
                //            m_PrefabRefData, m_PrefabObjectGeometryData, m_PrefabBuildingTerraformData, m_PrefabBuildingExtensionData,
                //            false, out _);
                //        return true;
                //    }

                //    lotInfo = default;
                //    return false;
                //}

                //private void UpdateSubNets(Transform transform, Transform mainTransform, Transform mainInverseTransform,
                //                           Entity prefab, Entity original, Entity lotEntity, bool relocate, bool topLevel,
                //                           OwnerDefinition ownerDefinition, NativeList<ClearAreaData> clearAreas, ref Random random) {
                //    var flag = original == Entity.Null || (relocate && m_EditorMode);
                //    if (flag && topLevel && m_PrefabSubNets.HasBuffer(prefab)) {
                //        var subNets = m_PrefabSubNets[prefab];
                //        var nodePositions = new NativeList<float4>(subNets.Length * 2, Allocator.Temp);
                //        var ownerLot = GetOwnerLot(lotEntity, out var lotInfo);
                //        for (var i = 0; i < subNets.Length; i++) {
                //            var subNet = subNets[i];
                //            if (subNet.m_NodeIndex.x >= 0) {
                //                while (nodePositions.Length <= subNet.m_NodeIndex.x) {
                //                    float4 value = default;
                //                    nodePositions.Add(in value);
                //                }

                //                nodePositions[subNet.m_NodeIndex.x] += new float4(subNet.m_Curve.a, 1f);
                //            }

                //            if (subNet.m_NodeIndex.y >= 0) {
                //                while (nodePositions.Length <= subNet.m_NodeIndex.y) {
                //                    float4 value = default;
                //                    nodePositions.Add(in value);
                //                }

                //                nodePositions[subNet.m_NodeIndex.y] += new float4(subNet.m_Curve.d, 1f);
                //            }
                //        }

                //        for (var j = 0; j < nodePositions.Length; j++)
                //            nodePositions[j] /= math.max(1f, nodePositions[j].w);

                //        for (var k = 0; k < subNets.Length; k++) {
                //            var subNet2 = NetUtils.GetSubNet(subNets, k, m_LefthandTraffic, ref m_PrefabNetGeometryData);
                //            CreateSubNet(
                //                subNet2.m_Prefab, Entity.Null, subNet2.m_Curve, subNet2.m_NodeIndex, subNet2.m_ParentMesh,
                //                subNet2.m_Upgrades, nodePositions, transform, ownerDefinition, clearAreas, lotInfo, ownerLot,
                //                ref random);
                //        }

                //        nodePositions.Dispose();
                //    }

                //    if (flag && topLevel && m_EditorMode && m_PrefabSubLanes.HasBuffer(prefab)) {
                //        var dynamicBuffer = m_PrefabSubLanes[prefab];
                //        var nodePositions2 = new NativeList<float4>(dynamicBuffer.Length * 2, Allocator.Temp);
                //        for (var l = 0; l < dynamicBuffer.Length; l++) {
                //            var subLane = dynamicBuffer[l];
                //            if (subLane.m_NodeIndex.x >= 0) {
                //                while (nodePositions2.Length <= subLane.m_NodeIndex.x) {
                //                    float4 value = default;
                //                    nodePositions2.Add(in value);
                //                }

                //                nodePositions2[subLane.m_NodeIndex.x] += new float4(subLane.m_Curve.a, 1f);
                //            }

                //            if (subLane.m_NodeIndex.y >= 0) {
                //                while (nodePositions2.Length <= subLane.m_NodeIndex.y) {
                //                    float4 value = default;
                //                    nodePositions2.Add(in value);
                //                }

                //                nodePositions2[subLane.m_NodeIndex.y] += new float4(subLane.m_Curve.d, 1f);
                //            }
                //        }

                //        for (var m = 0; m < nodePositions2.Length; m++)
                //            nodePositions2[m] /= math.max(1f, nodePositions2[m].w);

                //        for (var n = 0; n < dynamicBuffer.Length; n++) {
                //            var subLane2 = dynamicBuffer[n];
                //            CreateSubNet(
                //                Entity.Null, subLane2.m_Prefab, subLane2.m_Curve, subLane2.m_NodeIndex, subLane2.m_ParentMesh,
                //                default, nodePositions2, transform, ownerDefinition, clearAreas, default, false, ref random);
                //        }

                //        nodePositions2.Dispose();
                //    }

                //    if (!m_SubNets.HasBuffer(original)) {
                //        return;
                //    }

                //    var dynamicBuffer2 = m_SubNets[original];
                //    NativeHashMap<Entity, int> nativeHashMap = default;
                //    NativeList<float4> nodePositions3 = default;
                //    BuildingUtils.LotInfo lotInfo2 = default;
                //    var hasLot = false;
                //    if (!flag && relocate) {
                //        nativeHashMap = new NativeHashMap<Entity, int>(dynamicBuffer2.Length, Allocator.Temp);
                //        nodePositions3 = new NativeList<float4>(dynamicBuffer2.Length, Allocator.Temp);
                //        hasLot = GetOwnerLot(lotEntity, out lotInfo2);
                //        for (var num = 0; num < dynamicBuffer2.Length; num++) {
                //            var subNet3 = dynamicBuffer2[num].m_SubNet;
                //            if (m_NodeData.TryGetComponent(subNet3, out var componentData)) {
                //                if (nativeHashMap.TryAdd(subNet3, nodePositions3.Length)) {
                //                    componentData.m_Position = ObjectUtils.WorldToLocal(
                //                        mainInverseTransform, componentData.m_Position);
                //                    var value = new float4(componentData.m_Position, 1f);
                //                    nodePositions3.Add(in value);
                //                }
                //            } else if (m_EdgeData.TryGetComponent(subNet3, out var componentData2)) {
                //                if (nativeHashMap.TryAdd(componentData2.m_Start, nodePositions3.Length)) {
                //                    componentData.m_Position = ObjectUtils.WorldToLocal(
                //                        mainInverseTransform, m_NodeData[componentData2.m_Start].m_Position);
                //                    var value = new float4(componentData.m_Position, 1f);
                //                    nodePositions3.Add(in value);
                //                }

                //                if (nativeHashMap.TryAdd(componentData2.m_End, nodePositions3.Length)) {
                //                    componentData.m_Position = ObjectUtils.WorldToLocal(
                //                        mainInverseTransform, m_NodeData[componentData2.m_End].m_Position);
                //                    var value = new float4(componentData.m_Position, 1f);
                //                    nodePositions3.Add(in value);
                //                }
                //            }
                //        }
                //    }

                //    for (var num2 = 0; num2 < dynamicBuffer2.Length; num2++) {
                //        var subNet4 = dynamicBuffer2[num2].m_SubNet;
                //        if (m_NodeData.TryGetComponent(subNet4, out var componentData3)) {
                //            if (HasEdgeStartOrEnd(subNet4, original)) {
                //                continue;
                //            }

                //            var e = m_CommandBuffer.CreateEntity();
                //            CreationDefinition component = default;
                //            component.m_Original = subNet4;
                //            var flag2 = m_NetElevationData.TryGetComponent(subNet4, out var componentData4);
                //            var onGround = !flag2 || math.cmin(math.abs(componentData4.m_Elevation)) < 2f;
                //            if (flag || relocate || ClearAreaHelpers.ShouldClear(clearAreas, componentData3.m_Position, onGround)) {
                //                component.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                //            } else if (ownerDefinition.m_Prefab != Entity.Null) {
                //                m_CommandBuffer.AddComponent(e, ownerDefinition);
                //            }

                //            if (m_EditorContainerData.HasComponent(subNet4)) {
                //                component.m_SubPrefab = m_EditorContainerData[subNet4].m_Prefab;
                //            }

                //            m_CommandBuffer.AddComponent(e, component);
                //            m_CommandBuffer.AddComponent(e, default(Updated));
                //            NetCourse component2 = default;
                //            component2.m_Curve = new Bezier4x3(
                //                componentData3.m_Position, componentData3.m_Position, componentData3.m_Position,
                //                componentData3.m_Position);
                //            component2.m_Length = 0f;
                //            component2.m_FixedIndex = -1;
                //            component2.m_StartPosition.m_Entity = subNet4;
                //            component2.m_StartPosition.m_Position = componentData3.m_Position;
                //            component2.m_StartPosition.m_Rotation = componentData3.m_Rotation;
                //            component2.m_StartPosition.m_CourseDelta = 0f;
                //            component2.m_StartPosition.m_ParentMesh = -1;
                //            component2.m_EndPosition.m_Entity = subNet4;
                //            component2.m_EndPosition.m_Position = componentData3.m_Position;
                //            component2.m_EndPosition.m_Rotation = componentData3.m_Rotation;
                //            component2.m_EndPosition.m_CourseDelta = 1f;
                //            component2.m_EndPosition.m_ParentMesh = -1;
                //            m_CommandBuffer.AddComponent(e, component2);
                //            if (!flag && relocate) {
                //                Entity netPrefab = m_PrefabRefData[subNet4];
                //                componentData3.m_Position = ObjectUtils.WorldToLocal(mainInverseTransform, componentData3.m_Position);
                //                component2.m_Curve = new Bezier4x3(
                //                    componentData3.m_Position, componentData3.m_Position, componentData3.m_Position,
                //                    componentData3.m_Position);
                //                if (!flag2) {
                //                    component2.m_Curve.y = default;
                //                }

                //                var num3 = nativeHashMap[subNet4];
                //                var num4 = !flag2 ? -1 : 0;
                //                m_UpgradedData.TryGetComponent(subNet4, out var componentData5);
                //                CreateSubNet(
                //                    netPrefab, component.m_SubPrefab, component2.m_Curve, num3, num4, componentData5.m_Flags,
                //                    nodePositions3, mainTransform, ownerDefinition, clearAreas, lotInfo2, hasLot, ref random);
                //            }
                //        } else {
                //            if (!m_EdgeData.TryGetComponent(subNet4, out var componentData6)) {
                //                continue;
                //            }

                //            var e2 = m_CommandBuffer.CreateEntity();
                //            CreationDefinition component3 = default;
                //            component3.m_Original = subNet4;
                //            var curve = m_CurveData[subNet4];
                //            var flag3 = m_NetElevationData.TryGetComponent(subNet4, out var componentData7);
                //            var onGround2 = !flag3 || math.cmin(math.abs(componentData7.m_Elevation)) < 2f;
                //            if (flag || relocate || ClearAreaHelpers.ShouldClear(clearAreas, curve.m_Bezier, onGround2)) {
                //                component3.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                //            } else if (ownerDefinition.m_Prefab != Entity.Null) {
                //                m_CommandBuffer.AddComponent(e2, ownerDefinition);
                //            }

                //            if (m_EditorContainerData.HasComponent(subNet4)) {
                //                component3.m_SubPrefab = m_EditorContainerData[subNet4].m_Prefab;
                //            }

                //            m_CommandBuffer.AddComponent(e2, component3);
                //            m_CommandBuffer.AddComponent(e2, default(Updated));
                //            NetCourse component4 = default;
                //            component4.m_Curve = curve.m_Bezier;
                //            component4.m_Length = MathUtils.Length(component4.m_Curve);
                //            component4.m_FixedIndex = -1;
                //            component4.m_StartPosition.m_Entity = componentData6.m_Start;
                //            component4.m_StartPosition.m_Position = component4.m_Curve.a;
                //            component4.m_StartPosition.m_Rotation =
                //            NetUtils.GetNodeRotation(MathUtils.StartTangent(component4.m_Curve));
                //            component4.m_StartPosition.m_CourseDelta = 0f;
                //            component4.m_StartPosition.m_ParentMesh = -1;
                //            component4.m_EndPosition.m_Entity = componentData6.m_End;
                //            component4.m_EndPosition.m_Position = component4.m_Curve.d;
                //            component4.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(component4.m_Curve));
                //            component4.m_EndPosition.m_CourseDelta = 1f;
                //            component4.m_EndPosition.m_ParentMesh = -1;
                //            m_CommandBuffer.AddComponent(e2, component4);
                //            if (!flag && relocate) {
                //                Entity netPrefab2 = m_PrefabRefData[subNet4];
                //                component4.m_Curve.a = ObjectUtils.WorldToLocal(mainInverseTransform, component4.m_Curve.a);
                //                component4.m_Curve.b = ObjectUtils.WorldToLocal(mainInverseTransform, component4.m_Curve.b);
                //                component4.m_Curve.c = ObjectUtils.WorldToLocal(mainInverseTransform, component4.m_Curve.c);
                //                component4.m_Curve.d = ObjectUtils.WorldToLocal(mainInverseTransform, component4.m_Curve.d);
                //                if (!flag3) {
                //                    component4.m_Curve.y = default;
                //                }

                //                var nodeIndex = new int2(nativeHashMap[componentData6.m_Start], nativeHashMap[componentData6.m_End]);
                //                var parentMesh = new int2(
                //                    !m_NetElevationData.HasComponent(componentData6.m_Start) ? -1 : 0,
                //                    !m_NetElevationData.HasComponent(componentData6.m_End) ? -1 : 0);
                //                m_UpgradedData.TryGetComponent(subNet4, out var componentData8);
                //                CreateSubNet(
                //                    netPrefab2, component3.m_SubPrefab, component4.m_Curve, nodeIndex, parentMesh,
                //                    componentData8.m_Flags, nodePositions3, mainTransform, ownerDefinition, clearAreas, lotInfo2,
                //                    hasLot, ref random);
                //            }
                //        }
                //    }

                //    if (nativeHashMap.IsCreated) {
                //        nativeHashMap.Dispose();
                //    }

                //    if (nodePositions3.IsCreated) {
                //        nodePositions3.Dispose();
                //    }
                //}

                //private void UpdateSubAreas(Transform transform, Entity prefab, Entity original, bool relocate, bool rebuild,
                //                            bool topLevel, OwnerDefinition ownerDefinition, NativeList<ClearAreaData> clearAreas,
                //                            ref Random random, ref NativeParallelHashMap<Entity, int> selectedSpawnables) {
                //    var flag = original == Entity.Null || relocate || rebuild;
                //    if (flag && topLevel && m_PrefabSubAreas.HasBuffer(prefab)) {
                //        var dynamicBuffer = m_PrefabSubAreas[prefab];
                //        var dynamicBuffer2 = m_PrefabSubAreaNodes[prefab];
                //        for (var i = 0; i < dynamicBuffer.Length; i++) {
                //            var subArea = dynamicBuffer[i];
                //            int seed;
                //            if (!m_EditorMode && m_PrefabPlaceholderElements.HasBuffer(subArea.m_Prefab)) {
                //                var placeholderElements = m_PrefabPlaceholderElements[subArea.m_Prefab];
                //                if (!selectedSpawnables.IsCreated) {
                //                    selectedSpawnables = new NativeParallelHashMap<Entity, int>(10, Allocator.Temp);
                //                }

                //                if (!AreaUtils.SelectAreaPrefab(
                //                        placeholderElements, m_PrefabSpawnableObjectData, selectedSpawnables, ref random,
                //                        out subArea.m_Prefab, out seed)) {
                //                    continue;
                //                }
                //            } else {
                //                seed = random.NextInt();
                //            }

                //            var areaGeometryData = m_PrefabAreaGeometryData[subArea.m_Prefab];
                //            if (areaGeometryData.m_Type == AreaType.Space) {
                //                if (ClearAreaHelpers.ShouldClear(clearAreas, dynamicBuffer2, subArea.m_NodeRange, transform)) {
                //                    continue;
                //                }
                //            } else if (areaGeometryData.m_Type == AreaType.Lot && rebuild) {
                //                continue;
                //            }

                //            var e = m_CommandBuffer.CreateEntity();
                //            CreationDefinition component = default;
                //            component.m_Prefab = subArea.m_Prefab;
                //            component.m_RandomSeed = seed;
                //            if (areaGeometryData.m_Type != 0) {
                //                component.m_Flags |= CreationFlags.Hidden;
                //            }

                //            m_CommandBuffer.AddComponent(e, component);
                //            m_CommandBuffer.AddComponent(e, default(Updated));
                //            if (ownerDefinition.m_Prefab != Entity.Null) {
                //                m_CommandBuffer.AddComponent(e, ownerDefinition);
                //            }

                //            var dynamicBuffer3 = m_CommandBuffer.AddBuffer<Game.Areas.Node>(e);
                //            dynamicBuffer3.ResizeUninitialized(subArea.m_NodeRange.y - subArea.m_NodeRange.x + 1);
                //            DynamicBuffer<LocalNodeCache> dynamicBuffer4 = default;
                //            if (m_EditorMode) {
                //                dynamicBuffer4 = m_CommandBuffer.AddBuffer<LocalNodeCache>(e);
                //                dynamicBuffer4.ResizeUninitialized(dynamicBuffer3.Length);
                //            }

                //            var num = GetFirstNodeIndex(dynamicBuffer2, subArea.m_NodeRange);
                //            var num2 = 0;
                //            for (var j = subArea.m_NodeRange.x; j <= subArea.m_NodeRange.y; j++) {
                //                var position = dynamicBuffer2[num].m_Position;
                //                var position2 = ObjectUtils.LocalToWorld(transform, position);
                //                var parentMesh = dynamicBuffer2[num].m_ParentMesh;
                //                var elevation = math.select(float.MinValue, position.y, parentMesh >= 0);
                //                dynamicBuffer3[num2] = new Game.Areas.Node(position2, elevation);
                //                if (m_EditorMode) {
                //                    dynamicBuffer4[num2] = new LocalNodeCache {
                //                        m_Position = position,
                //                        m_ParentMesh = parentMesh,
                //                    };
                //                }

                //                num2++;
                //                if (++num == subArea.m_NodeRange.y) {
                //                    num = subArea.m_NodeRange.x;
                //                }
                //            }
                //        }
                //    }

                //    if (!m_SubAreas.HasBuffer(original)) {
                //        return;
                //    }

                //    var dynamicBuffer5 = m_SubAreas[original];
                //    for (var k = 0; k < dynamicBuffer5.Length; k++) {
                //        var area = dynamicBuffer5[k].m_Area;
                //        var nodes = m_AreaNodes[area];
                //        var flag2 = flag;
                //        if (!flag2 && m_AreaSpaceData.HasComponent(area)) {
                //            var triangles = m_AreaTriangles[area];
                //            flag2 = ClearAreaHelpers.ShouldClear(clearAreas, nodes, triangles, transform);
                //        }

                //        if (m_AreaLotData.HasComponent(area)) {
                //            if (!flag2) {
                //                continue;
                //            }

                //            flag2 = !rebuild;
                //        }

                //        var e2 = m_CommandBuffer.CreateEntity();
                //        CreationDefinition component2 = default;
                //        component2.m_Original = area;
                //        if (flag2) {
                //            component2.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                //        } else if (ownerDefinition.m_Prefab != Entity.Null) {
                //            m_CommandBuffer.AddComponent(e2, ownerDefinition);
                //        }

                //        m_CommandBuffer.AddComponent(e2, component2);
                //        m_CommandBuffer.AddComponent(e2, default(Updated));
                //        m_CommandBuffer.AddBuffer<Game.Areas.Node>(e2).CopyFrom(nodes.AsNativeArray());
                //        if (m_CachedNodes.HasBuffer(area)) {
                //            var dynamicBuffer6 = m_CachedNodes[area];
                //            m_CommandBuffer.AddBuffer<LocalNodeCache>(e2).CopyFrom(dynamicBuffer6.AsNativeArray());
                //        }
                //    }
                //}

                //private bool HasEdgeStartOrEnd(Entity node, Entity owner) {
                //    var dynamicBuffer = m_ConnectedEdges[node];
                //    for (var i = 0; i < dynamicBuffer.Length; i++) {
                //        var edge = dynamicBuffer[i].m_Edge;
                //        var edge2 = m_EdgeData[edge];
                //        if ((edge2.m_Start == node || edge2.m_End == node) && m_OwnerData.HasComponent(edge) &&
                //            m_OwnerData[edge].m_Owner == owner) {
                //            return true;
                //        }
                //    }

                //    return false;
                //}

                //private struct VariationData {
                //    public Entity m_Prefab;

                //    public int m_Probability;
                //}
            }
    }
}