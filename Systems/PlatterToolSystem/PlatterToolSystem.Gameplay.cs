// <copyright file="PlatterToolSystem.Gameplay.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Json;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Platter.Constants;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Transform = Game.Objects.Transform;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        /// <inheritdoc/>
        public override bool TrySetPrefab(Game.Prefabs.PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab(prefab {prefab}) -- Active tool: ${m_ToolSystem.activeTool}");

            if (m_ToolSystem.activeTool != this || prefab is not ParcelPrefab parcelPrefab) {
                return false;
            }

            SelectedPrefab = parcelPrefab;
            m_RoadEditorData.RecalculatePoints = true;
            return true;
        }

        /// <inheritdoc/>
        public override void InitializeRaycast() {
            base.InitializeRaycast();
            var currentMode = CurrentMode;

            // if (this.m_Prefab == null) {
            //    this.m_ToolRaycastSystem.typeMask = TypeMask.Terrain | TypeMask.Water;
            //    this.m_ToolRaycastSystem.raycastFlags |= RaycastFlags.Outside;
            //    this.m_ToolRaycastSystem.netLayerMask = Layer.None;
            //    this.m_ToolRaycastSystem.rayOffset = default;
            //    return;
            // }

            // GetAvailableSnapMask(out var onMask, out var offMask);
            // var actualSnap = ToolBaseSystem.GetActualSnap(this.selectedSnap, onMask, offMask);
            switch (currentMode) {
                case PlatterToolMode.Plop:
                    // if (!TryGetRayOffset(out var rayOffset)) {
                    //    return;
                    // }
                    m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround;
                    m_ToolRaycastSystem.typeMask = TypeMask.Lanes | TypeMask.Net;
                    m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger;
                    m_ToolRaycastSystem.netLayerMask = Layer.All;
                    m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
                    m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;

                    // m_ToolRaycastSystem.rayOffset = rayOffset;
                    break;
                case PlatterToolMode.Brush:
                    m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround;
                    m_ToolRaycastSystem.typeMask = TypeMask.Lanes | TypeMask.Net;
                    m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger;
                    m_ToolRaycastSystem.netLayerMask = Layer.All;
                    m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
                    m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
                    break;
                case PlatterToolMode.RoadEdge:
                    m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround;
                    m_ToolRaycastSystem.typeMask = TypeMask.Lanes | TypeMask.Net;
                    m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements | RaycastFlags.Cargo | RaycastFlags.Passenger;
                    m_ToolRaycastSystem.netLayerMask = Layer.Road;
                    m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
                    m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
                    break;
                default:
                    break;
            }
        }

        public bool TryGetRayOffset(out float3 rayOffset) {
            rayOffset = default;

            if (this.m_PrefabSystem.TryGetComponentData<PlaceableObjectData>(this.m_Prefab, out var placeableObjectData)) {
                rayOffset -= placeableObjectData.m_PlacementOffset;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        // public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask) {
        //    base.GetAvailableSnapMask(out onMask, out offMask);

        // PlatterToolMode actualMode = ActualToolMode;

        // switch (actualMode) {
        //        case PlatterToolMode.Point:
        //            onMask |= Snap.NetSide;
        //            offMask |= Snap.NetSide;
        //            break;
        //        case PlatterToolMode.Brush:
        //            onMask |= Snap.NetSide;
        //            offMask |= Snap.NetSide;
        //            break;
        //        case PlatterToolMode.RoadEdge:
        //            onMask |= Snap.NetSide;
        //            offMask |= Snap.NetSide;
        //            break;
        //        default:
        //            break;
        //    }
        // }

        /// <inheritdoc/>
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            // Clear state.
            applyMode = ApplyMode.Clear;

            m_InputDeps = base.OnUpdate(inputDeps);
            var currentMode = CurrentMode;

            return currentMode switch {
                PlatterToolMode.Plop => HandlePlopUpdate(inputDeps),
                PlatterToolMode.Brush => HandleBrushUpdate(inputDeps),
                PlatterToolMode.RoadEdge => HandleRoadEditorUpdate(inputDeps),
                _ => inputDeps,
            };
        }

        public JobHandle HandlePlopUpdate(JobHandle inputDeps) {
            if (m_Prefab == null || CurrentMode != PlatterToolMode.Plop) {
                return inputDeps;
            }

            if (GetRaycastResult(out ControlPoint currentControlPoint, out var raycastForceUpdate)) {
                // Valid raycast - update position.
                var position = currentControlPoint.m_HitPosition;
                var rotation = currentControlPoint.m_Rotation;

                // Calculate terrain height.
                var terrainHeightData = m_TerrainSystem.GetHeightData();
                position.y = TerrainUtils.SampleHeight(ref terrainHeightData, position);

                // Don't update if the cursor hasn't moved.
                if (position.x != m_LastCursorPosition.x || position.z != m_LastCursorPosition.z) {
                    MakeCreateDefinitionsJob(
                        m_SelectedEntity,
                        position,
                        rotation,
                        RandomSeed.Next()
                    );
                } else {
                    applyMode = ApplyMode.None;
                }

                // Update references
                m_LastRaycastPoint = currentControlPoint;
                m_LastCursorPosition = position;
            }

            return inputDeps;
        }

        public JobHandle HandleBrushUpdate(JobHandle inputDeps) {
            return inputDeps;
        }

        public void ResetRoadEditor() {
            m_Log.Debug($"ResetRoadEditor() -- Road Editor reset requested.");
            m_RoadEditorData.Reset();
        }

        public void Reset() {
            m_Log.Debug($"Reset() -- Tool reset requested.");
            ChangeHighlighting(m_RoadEditorData.SelectedEdgeEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
            m_HoveredEntity = Entity.Null;
            m_LastHitPosition = float3.zero;
            m_Points.Clear();
            ResetRoadEditor();
        }

        /// <summary>
        /// Update loop for Road Editor Tool mode.
        ///
        /// In Road Editor Mode, our job is to <br/>
        ///     1. Handle the selection of a Road Entity. <br/>
        ///     2. When a Road Entity is selected, <br/>
        ///         2a. Handle any tool configuration and preview them in-game <br/>
        ///         2b. Handle apply <br/>
        ///         2c. Handle cancellation. <br/>
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns>inputDeps.</returns>
        public JobHandle HandleRoadEditorUpdate(JobHandle inputDeps) {
            const string logPrefix = "HandleRoadEditorUpdate()";

            // ###
            // First, let's handle any "create" actions requested
            // ###
            if (m_CreateAction.WasPressedThisFrame() || ApplyWasRequested) {
                m_Log.Debug($"{logPrefix} -- Create action.");
                m_Log.Debug($"{logPrefix} -- m_CreateAction.WasPressedThisFrame(): {m_CreateAction.WasPressedThisFrame()}");
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
                m_HoveredEntity = entity;
                m_LastHitPosition = raycastHit.m_HitPosition;

                // Check for apply action initiation.
                var applyWasPressed = m_ApplyAction.WasPressedThisFrame();

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
            } else if (m_CancelAction.WasPressedThisFrame() ||
                (m_RoadEditorData.SelectedEdgeEntity != Entity.Null && !EntityManager.Exists(m_RoadEditorData.SelectedEdgeEntity))) {
                m_Log.Debug($"{logPrefix} -- Cancel action.");

                // Right click & we had something selected -> deselect and reset the tool.
                Reset();
            } else if (m_HoveredEntity != Entity.Null) {
                // No raycast hit, no action pressed, remove hover from any entity that was being hovered before.
                if (m_HoveredEntity != m_RoadEditorData.SelectedEdgeEntity) {
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                }

                m_HoveredEntity = Entity.Null;
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

            var rotation = 1; // Demo data
            var parcelWidth = m_SelectedParcelSize.x * DimensionConstants.CellSize;

            m_Log.Debug($"Calculating parcel points.");
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
                m_OverlayBuffer,
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

        public void RequestApply() {
            applyMode = ApplyMode.Apply;
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

        private JobHandle UpdateDefinitions(JobHandle inputDeps) {
            var jobHandle = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);

            if (m_Prefab == null) {
                return jobHandle;
            }

            var entity = m_PrefabSystem.GetEntity(m_Prefab);
            var laneContainer = Entity.Null;
            var transformPrefab = Entity.Null;
            var brushPrefab = Entity.Null;
            var deltaTime = global::UnityEngine.Time.deltaTime;
            var controlPoint = m_ControlPoints[0];
            var randomSeed = RandomSeed.Next();

            // jobHandle = JobHandle.CombineDependencies(
            //    jobHandle,
            //    MakeCreateDefinitionsJob(entity, controlPoint.m_Position, controlPoint.m_Rotation,
            //        randomSeed));

            // MakeCreateDefinitionsJob(entity, transformPrefab, brushPrefab, m_UpgradingObject,
            //       m_MovingObject, laneContainer, m_CityConfigurationSystem.defaultTheme,
            //       m_ControlPoints, attachmentPrefab, m_ToolSystem.actionMode.IsEditor(),
            //       m_CityConfigurationSystem.leftHandTraffic, m_State == State.Removing,
            //       actualMode == Mode.Stamp, brushSize, math.radians(brushAngle),
            //       brushStrength, deltaTime, m_RandomSeed, GetActualSnap(), actualAgeMask,
            //       inputDeps));
            return jobHandle;
        }

        /// <summary>
        /// Creates temporary object definitions for previewing.
        /// </summary>
        /// <param name="objectPrefab">Object prefab entity.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="rotation">Entity rotation.</param>
        /// <param name="randomSeed">Random seed to use.</param>
        private void MakeCreateDefinitionsJob(Entity objectPrefab, float3 position, quaternion rotation, RandomSeed randomSeed) {
            var createDefJob = new CreateDefinitions() {
                m_EditorMode = m_ToolSystem.actionMode.IsEditor(),
                m_LefthandTraffic = m_CityConfigurationSystem.leftHandTraffic,
                m_ObjectPrefab = objectPrefab,
                m_Theme = m_CityConfigurationSystem.defaultTheme,
                m_RandomSeed = randomSeed,
                m_ControlPoint = new() {
                    m_Position = position,
                    m_Rotation = rotation
                },
                m_AttachmentPrefab = default,
                m_OwnerData = GetComponentLookup<Owner>(true),
                m_TransformData = GetComponentLookup<Transform>(true),
                m_AttachedData = GetComponentLookup<Attached>(true),
                m_LocalTransformCacheData = GetComponentLookup<LocalTransformCache>(true),
                m_ElevationData = GetComponentLookup<Game.Objects.Elevation>(true),
                m_BuildingData = GetComponentLookup<Building>(true),
                m_LotData = GetComponentLookup<Game.Buildings.Lot>(true),
                m_EdgeData = GetComponentLookup<Edge>(true),
                m_NodeData = GetComponentLookup<Game.Net.Node>(true),
                m_CurveData = GetComponentLookup<Curve>(true),
                m_NetElevationData = GetComponentLookup<Game.Net.Elevation>(true),
                m_OrphanData = GetComponentLookup<Orphan>(true),
                m_UpgradedData = GetComponentLookup<Upgraded>(true),
                m_CompositionData = GetComponentLookup<Composition>(true),
                m_AreaClearData = GetComponentLookup<Clear>(true),
                m_AreaSpaceData = GetComponentLookup<Space>(true),
                m_AreaLotData = GetComponentLookup<Game.Areas.Lot>(true),
                m_EditorContainerData =
                    GetComponentLookup<Game.Tools.EditorContainer>(true),
                m_PrefabRefData = GetComponentLookup<PrefabRef>(true),
                m_PrefabNetObjectData = GetComponentLookup<NetObjectData>(true),
                m_PrefabBuildingData = GetComponentLookup<BuildingData>(true),
                m_PrefabAssetStampData = GetComponentLookup<AssetStampData>(true),
                m_PrefabBuildingExtensionData =
                    GetComponentLookup<BuildingExtensionData>(true),
                m_PrefabSpawnableObjectData =
                    GetComponentLookup<SpawnableObjectData>(true),
                m_PrefabObjectGeometryData = GetComponentLookup<ObjectGeometryData>(true),
                m_PrefabPlaceableObjectData =
                    GetComponentLookup<PlaceableObjectData>(true),
                m_PrefabAreaGeometryData = GetComponentLookup<AreaGeometryData>(true),
                m_PrefabBuildingTerraformData =
                    GetComponentLookup<BuildingTerraformData>(true),
                m_PrefabCreatureSpawnData = GetComponentLookup<CreatureSpawnData>(true),
                m_PlaceholderBuildingData =
                    GetComponentLookup<PlaceholderBuildingData>(true),
                m_PrefabNetGeometryData = GetComponentLookup<NetGeometryData>(true),
                m_PrefabCompositionData = GetComponentLookup<NetCompositionData>(true),
                m_SubObjects = GetBufferLookup<Game.Objects.SubObject>(true),
                m_CachedNodes = GetBufferLookup<LocalNodeCache>(true),
                m_InstalledUpgrades = GetBufferLookup<InstalledUpgrade>(true),
                m_SubNets = GetBufferLookup<Game.Net.SubNet>(true),
                m_ConnectedEdges = GetBufferLookup<ConnectedEdge>(true),
                m_SubAreas = GetBufferLookup<Game.Areas.SubArea>(true),
                m_AreaNodes = GetBufferLookup<Game.Areas.Node>(true),
                m_AreaTriangles = GetBufferLookup<Triangle>(true),
                m_PrefabSubObjects = GetBufferLookup<Game.Prefabs.SubObject>(true),
                m_PrefabSubNets = GetBufferLookup<Game.Prefabs.SubNet>(true),
                m_PrefabSubLanes = GetBufferLookup<Game.Prefabs.SubLane>(true),
                m_PrefabSubAreas = GetBufferLookup<Game.Prefabs.SubArea>(true),
                m_PrefabSubAreaNodes = GetBufferLookup<SubAreaNode>(true),
                m_PrefabPlaceholderElements =
                    GetBufferLookup<PlaceholderObjectElement>(true),
                m_PrefabRequirementElements =
                    GetBufferLookup<ObjectRequirementElement>(true),
                m_PrefabServiceUpgradeBuilding =
                    GetBufferLookup<ServiceUpgradeBuilding>(true),
                m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var _),
                m_TerrainHeightData = m_TerrainSystem.GetHeightData(),
                m_CommandBuffer = m_ToolOutputBarrier.CreateCommandBuffer(),
            };

            createDefJob.Execute();
        }
    }
}
