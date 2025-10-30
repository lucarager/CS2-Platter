// <copyright file="P_ToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using Colossal.Entities;
    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Pathfind;
    using Game.Prefabs;
    using Game.PSI;
    using Game.Rendering;
    using Game.Tools;
    using Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.Internal;
    using Unity.Jobs;
    using Unity.Mathematics;
    using NetSearchSystem = Game.Net.SearchSystem;
    using ZonesSearchSystem = Game.Zones.SearchSystem;

    /// <summary>
    /// New in-progress system to handle plopping parcels directly.
    /// Currently not enabled.
    /// </summary>
    public partial class P_ToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Instance.
        /// </summary>
        public static P_ToolSystem Instance;

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

        // Data
        private NativeReference<NetToolSystem.AppliedUpgrade> m_AppliedUpgrade;
        private float3                                        m_RotationStartPosition;
        private bool                                          m_ApplyBlocked;
        private NativeList<ControlPoint>                      m_ControlPoints;
        private EntityQuery                                   m_DefinitionQuery;
        private bool                                          m_ForceCancel;
        private NativeReference<Rotation>                     m_Rotation;
        private bool                                          m_RotationModified;
        private Entity                                        m_SelectedEntity = Entity.Null;
        private ParcelPrefab                                  m_Prefab;
        private RandomSeed                                    m_RandomSeed;
        private Mode                                          m_LastToolMode;
        private ControlPoint                                  m_LastRaycastPoint;
        private Entity                                        m_MovingInitialized;
        private Snap                                          m_SelectedSnap;
        private ControlPoint                                  m_StartPoint;
        private quaternion                                    m_StartRotation;
        private State                                         m_State;
        private EntityQuery                                   m_TempQuery;
        private Mode                                          m_ToolMode;
        private NativeList<NetToolSystem.UpgradeState>        m_UpgradeStates;
        private ObjectPrefab                                  m_SelectedPrefab;

        // Queries
        private EntityQuery  m_HighlightedQuery;

        // Logger
        private PrefixedLogger  m_Log;

        // Actions
        private IProxyAction m_PreciseRotation;

        // Systems & References
        private RaycastSystem      m_RaycastSystem;
        private CameraUpdateSystem m_CameraUpdateSystem;
        private NetSearchSystem    m_NetSearchSystem;
        private P_OverlaySystem    m_PlatterOverlaySystem;
        private ZonesSearchSystem  m_ZoneSearchSystem;

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

        /// <inheritdoc/>
        protected override void OnCreate() {
            // References & State
            Instance = this;
            Enabled  = false;

            base.OnCreate();

            // Logging
            m_Log = new PrefixedLogger(nameof(P_ToolSystem));
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

            // Actions
            //m_PreciseRotation = InputManager.instance.toolActionCollection.GetActionState("Precise Rotation",
            // "ObjectToolSystem");
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

            // Clear any applications.
            applyMode = ApplyMode.Clear;

            // Manually enable base action if in editor mode.
            if (m_ToolSystem.actionMode.IsEditor()) {
                //applyAction.enabled = true;
            }

            // Visualization
            requireZones       = true;
            requireAreas       = AreaTypeMask.Lots;
            requireUnderground = false;
            requireNetArrows   = false;

            // Data
            m_ForceCancel          = false;
            m_ApplyBlocked         = false;
            m_LastRaycastPoint     = default;
            m_AppliedUpgrade.Value = default;
            m_State                = State.Default;
            m_MovingInitialized    = Entity.Null;
            m_ForceCancel          = false;
            m_ApplyBlocked         = false;
            Randomize();
        }

        /// <inheritdoc/>
        protected override void OnStopRunning() {
            base.OnStopRunning();
            m_Log.Debug("OnStopRunning()");
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
            // If this tool is not the active tool, clear UI state and bail out.
            if (m_ToolSystem.activeTool != this) {
                return Clear(inputDeps);
            }

            // Update control points based on tool mode
            if (m_LastToolMode != m_ToolMode) {
                var maxControlPointCount = GetMaxControlPointCount();
                if (maxControlPointCount < m_ControlPoints.Length) {
                    m_ControlPoints.RemoveRange(maxControlPointCount, m_ControlPoints.Length - maxControlPointCount);
                }

                m_LastToolMode = m_ToolMode;
            }

            // Update flags based on prefab selection
            if (m_SelectedPrefab != null) {
                // Update flags
                allowUnderground   = false;
                requireUnderground = false;
                requireNet         = Layer.None;
                requireNetArrows   = false;
                requireStops       = TransportType.None;
                requireUnderground = false;
                requireZones       = true;

                UpdateInfoview(m_ToolSystem.actionMode.IsEditor() ? Entity.Null : m_PrefabSystem.GetEntity(m_Prefab));
                GetAvailableSnapMask(out m_SnapOnMask, out m_SnapOffMask);

                m_PrefabSystem.TryGetComponentData<ObjectGeometryData>(m_Prefab, out var objectGeometryData);
                m_PrefabSystem.TryGetComponentData<PlaceableObjectData>(m_Prefab, out var placeableObjectData);
            } else {
                requireUnderground = false;
                requireZones       = false;
                requireNetArrows   = false;
                requireNet         = Layer.None;
                UpdateInfoview(Entity.Null);
            }

            // Update state
            if (m_SelectedPrefab != null) {
                if (m_State != State.Default && !applyAction.enabled && !secondaryApplyAction.enabled) {
                    m_State = State.Default;
                }
            } else {
                if ((m_State == State.Adding && (applyAction.WasReleasedThisFrame() || cancelAction.WasPressedThisFrame())) ||
                    (m_State == State.Removing &&
                     (secondaryApplyAction.WasReleasedThisFrame() || cancelAction.WasPressedThisFrame())) ||
                    (m_State != State.Default &&
                     (applyAction.WasReleasedThisFrame() || secondaryApplyAction.WasReleasedThisFrame()))) {
                    m_State = State.Default;
                }
            }

            // Handle actions
            const RaycastFlags disabledFlags    = RaycastFlags.DebugDisable | RaycastFlags.UIDisable;
            var                isRaycastEnabled = (m_ToolRaycastSystem.raycastFlags & disabledFlags) == 0;

            if (isRaycastEnabled) {
                return isUpgradeMode ? HandleUpgradeMode(inputDeps) : HandleToolInput(inputDeps, m_ForceCancel, m_ToolMode);
            }

            if (cancelAction.WasPressedThisFrame()) {
                return Cancel(inputDeps, cancelAction.WasReleasedThisFrame());
            }

            if (applyAction.WasPressedThisFrame()) {
                var jobHandle = Apply(inputDeps, cancelAction.WasReleasedThisFrame());
                ResetTool();
                return jobHandle;
            }

            // Return update
            return Clear(inputDeps);
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

        private JobHandle SnapControlPoint(JobHandle inputDeps) {
            return inputDeps;
        }

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
            //        findAttachmentBuildingJob.m_BuildingDataType = InternalCompilerInterface.GetComponentTypeHandle<BuildingData>(
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
            //            m_ToolSystem.actionMode.IsEditor(), m_CityConfigurationSystem.leftHandTraffic, m_State == State.Removing,
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

        private struct Rotation {
            public quaternion m_Rotation;

            public quaternion m_ParentRotation;

            public bool m_IsAligned;

            public bool m_IsSnapped;
        }
    }
}