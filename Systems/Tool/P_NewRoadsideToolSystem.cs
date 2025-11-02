// <copyright file="P_NewRoadsideToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Linq;

namespace Platter.Systems {
    using System;
    using System.Collections.Generic;
    using Colossal.Entities;
    using Colossal.Json;
    using Colossal.Mathematics;
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
    using Game.PSI;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Platter.Constants;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Entities.Internal;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Utils;
    using static Platter.Systems.P_ParcelCreationDefinitionJob;
    using static Platter.Systems.P_RoadsideToolSystem;
    using Color = UnityEngine.Color;
    using NetSearchSystem = Game.Net.SearchSystem;
    using Transform = Game.Objects.Transform;
    using ZonesSearchSystem = Game.Zones.SearchSystem;

    /// <summary>
    /// New in-progress system to handle plopping parcels directly.
    /// Currently not enabled.
    /// </summary>
    public partial class P_NewRoadsideToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Instance.
        /// </summary>
        public static P_NewRoadsideToolSystem Instance;

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

        public int2 SelectedParcelSize {
            get => m_SelectedParcelSize;
            set {
                m_SelectedParcelSize               = value;
                m_Data.RecalculatePoints = true;
            }
        }

        public float RoadEditorSpacing {
            get => m_RoadEditorSpacing;
            set {
                m_RoadEditorSpacing                = value;
                m_Data.RecalculatePoints = true;
            }
        }

        public float RoadEditorOffset {
            get => m_RoadEditorOffset;
            set {
                m_Data.RecalculatePoints = true;
                m_RoadEditorOffset                 = value;
            }
        }

        public bool4 RoadEditorSides {
            get => m_RoadEditorSides;
            set {
                m_RoadEditorSides                  = value;
                m_Data.RecalculatePoints = true;
            }
        }

        // Data
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
        private EntityQuery                                   m_HighlightedQuery;
        public  Entity                                        m_HoveredEntity;
        private float3                                        m_LastCursorPosition;
        public  float3                                        m_LastHitPosition;
        private RoadsideData                                  m_Data;
        public  bool                                          ApplyWasRequested;
        public  List<Transform>                               m_Points;
        private int2                                          m_SelectedParcelSize = new(2, 2);
        private float                                         m_RoadEditorOffset   = 2f;
        private bool4                                         m_RoadEditorSides    = new(true, true, false, false);
        private float                                         m_RoadEditorSpacing  = 1f;

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
            m_Log = new PrefixedLogger(nameof(P_NewRoadsideToolSystem));
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
            m_State                = State.Default;
            m_MovingInitialized    = Entity.Null;
            m_ForceCancel          = false;
            m_ApplyBlocked         = false;

            Randomize();
        }

        private void UpdateSelectedPrefab() {
            // Todo abstract this
            var id = new PrefabID("ParcelPrefab", $"Parcel {m_SelectedParcelSize.x}x{m_SelectedParcelSize.y}");

            if (!m_PrefabSystem.TryGetPrefab(id, out var prefabBase)) {
                m_Log.Debug($"UpdateSelectedPrefab() -- Couldn't find prefabBase!");
                return;
            }
            TrySetPrefab(prefabBase);
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
            const string logPrefix = "OnUpdate()";


            // If this tool is not the active tool, clear UI state and bail out.
            if (m_ToolSystem.activeTool != this) {
                return Clear(inputDeps);
            }

            // ###
            // [] This part of the code handles creating parcels when requested
            // ###
            if (ApplyWasRequested) {
                m_Log.Debug($"{logPrefix} -- Create action.");
                m_Log.Debug($"{logPrefix} -- ApplyWasRequested: {ApplyWasRequested}");

                // Reset
                ApplyWasRequested = false;

                // Validate
                if (m_Data.SelectedEdgeEntity == Entity.Null || 
                    m_SelectedEntity == Entity.Null) {
                    m_Log.Debug($"{logPrefix} -- Create action invalid, aborting.");
                    m_Log.Debug($"{logPrefix} -- SelectedEdgeEntity: {m_Data.SelectedEdgeEntity}");
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
            // This part of the code is about handling raycast hits and action presses for edge selection
            // ###
            HandleRoadEdgeSelection();

            // Exit if we do not have a road selected
            if (m_Data.SelectedEdgeEntity == Entity.Null) {
                return inputDeps;
            }

            // Exit if we do not need to recalculate points at this stage.
            if (!m_Data.RecalculatePoints) {
                applyMode = ApplyMode.None; // This will "keep" temp entities
                return inputDeps;
            }

            // ###
            // This part of the code handles the creation of entities.
            // ###
            return HandleTempParcelCreation(inputDeps);
        }

        private void Reset() {
            m_Log.Debug("Reset() -- Tool reset requested.");
            ChangeHighlighting(m_Data.SelectedEdgeEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
            m_HoveredEntity   = Entity.Null;
            m_LastHitPosition = float3.zero;
            m_Data.Reset();
        }

        private JobHandle HandleTempParcelCreation(JobHandle inputDeps) {
            // todo move rendering to system
            var buffer = World.GetOrCreateSystemManaged<OverlayRenderSystem>().GetBuffer(out var bufferJobHandle);
            inputDeps = JobHandle.CombineDependencies(inputDeps, bufferJobHandle);
            var terrainHeightData = m_TerrainSystem.GetHeightData();
            m_Points.Clear();

            var rotation    = 1; // Demo data
            var parcelWidth = m_SelectedParcelSize.x * DimensionConstants.CellSize;

            m_Log.Debug("Calculating parcel points.");
            m_Log.Debug($"RoadEditorSpacing ${RoadEditorSpacing.ToJSONString()}");
            m_Log.Debug($"RoadEditorOffset ${RoadEditorOffset.ToJSONString()}");
            m_Log.Debug($"RoadEditorSides ${RoadEditorSides.ToJSONString()}");

            m_Data.CalculatePoints(
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
                CreateParcelCreationJob(
                    m_SelectedEntity,
                    transformData.m_Position,
                    transformData.m_Rotation,
                    RandomSeed.Next()
                );
            }

            return inputDeps;
        }

        private void HandleRoadEdgeSelection() {
            if (GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                // Store results
                var previousHoveredEntity = m_HoveredEntity;
                m_HoveredEntity   = entity;
                m_LastHitPosition = raycastHit.m_HitPosition;

                if (applyAction.WasPressedThisFrame()) {
                    if (m_Data.Start(entity)) {
                        m_Log.Debug($"Selected entity: {entity}");
                        m_Data.RecalculatePoints = true;
                        SwapHighlitedEntities(m_Data.SelectedEdgeEntity, entity);
                    }
                } else if (previousHoveredEntity != m_HoveredEntity) {
                    // If we were previously hovering over an Edge that isn't the selected one, unhighlight it.
                    if (previousHoveredEntity != m_Data.SelectedEdgeEntity) {
                        ChangeHighlighting(previousHoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                    }

                    // Highlight the hovered entity
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.AddHighlight);
                }
            } else if (cancelAction.WasPressedThisFrame() ||
                       (m_Data.SelectedEdgeEntity != Entity.Null &&
                        !EntityManager.Exists(m_Data.SelectedEdgeEntity))) {
                m_Log.Debug($"Cancel action.");

                // Right click & we had something selected -> deselect and reset the tool.
                Reset();
            } else if (m_HoveredEntity != Entity.Null) {
                // No raycast hit, no action pressed,
                // remove hover from any entity that was being hovered before.
                if (m_HoveredEntity != m_Data.SelectedEdgeEntity) {
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                }

                m_HoveredEntity   = Entity.Null;
                m_LastHitPosition = float3.zero;
            }
        }

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

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            return inputDeps;
        }

        private void CreateParcelCreationJob(Entity objectPrefab, float3 position, quaternion rotation, RandomSeed randomSeed) {
            var createDefJob = new ParcelCreationDefinitionJob(
                objectPrefab: objectPrefab,
                randomSeed: randomSeed,
                controlPoint: new ControlPoint {
                    m_Position = position,
                    m_Rotation = rotation,
                },
                transformLookup: SystemAPI.GetComponentLookup<Transform>(true),
                elevationLookup: SystemAPI.GetComponentLookup<Game.Objects.Elevation>(true),
                commandBuffer: m_ToolOutputBarrier.CreateCommandBuffer()
            );

            createDefJob.Execute();
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

        public class RoadsideData {
            public  Entity             SelectedEdgeEntity    { get; set; }
            public  Curve              SelectedCurve         { get; set; }
            public  NetCompositionData SelectedCurveGeo      { get; set; }
            public  EdgeGeometry       SelectedCurveEdgeGeo  { get; set; }
            public  StartNodeGeometry  SelectedCurveStartGeo { get; set; }
            public  EndNodeGeometry    SelectedCurveEndGeo   { get; set; }
            public  PrefabBase         SelectedPrefabBase    { get; set; }
            public  bool               RecalculatePoints     { get; set; }
            private EntityManager      m_EntityManager;
            private PrefabSystem       m_PrefabSystem;

            public RoadsideData(EntityManager entityManager, PrefabSystem prefabSystem) {
                m_EntityManager = entityManager;
                m_PrefabSystem  = prefabSystem;
            }

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
        public class CurveData {
            public Bezier4x3 Curve;
            public float     Direction = 1;
            public float3    EndPointLocation;
            public float     Length;
            public float     RelativeStartingPosition;
            public float3    StartPointLocation;

            public CurveData(Bezier4x3 curve, float direction) {
                Curve     = curve;
                Length    = MathUtils.Length(curve);
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
    }
}