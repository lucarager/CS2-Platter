// <copyright file="PlatterToolSystem.Gameplay.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Constants;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Transform = Game.Objects.Transform;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        /// <inheritdoc/>
        public override bool TrySetPrefab(Game.Prefabs.PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab(prefab {prefab})");

            m_Log.Debug($"TrySetPrefab(prefab {prefab}) -- prefab is ObjectPrefab {prefab is ObjectPrefab}");
            m_Log.Debug($"TrySetPrefab(prefab {prefab}) -- prefab is ParcelPrefab {prefab is ParcelPrefab}");

            if (m_ToolSystem.activeTool == this && prefab is ObjectPrefab objectPrefab) {
                SelectedPrefab = objectPrefab;
                return true;
            }

            return false;
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
                case PlatterToolMode.Point:
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
                PlatterToolMode.Point => HandleCreateUpdate(inputDeps),
                PlatterToolMode.Brush => HandleBrushUpdate(inputDeps),
                PlatterToolMode.RoadEdge => HandleRoadEdgeUpdate(inputDeps),
                _ => inputDeps,
            };
        }

        public JobHandle HandleCreateUpdate(JobHandle inputDeps) {
            // GetAvailableSnapMask(out this.m_SnapOnMask, out this.m_SnapOffMask);
            return inputDeps;
        }

        public JobHandle HandleBrushUpdate(JobHandle inputDeps) {
            // GetAvailableSnapMask(out this.m_SnapOnMask, out this.m_SnapOffMask);
            return inputDeps;
        }

        /// <summary>
        /// Update loop for Road Edge Tool mode.
        ///
        /// In Road Edge Mode, our job is to <br/>
        ///     1. Handle the selection of a Road Entity. <br/>
        ///     2. When a Road Entity is selected, <br/>
        ///         2a. Handle any tool configuration and preview them in-game <br/>
        ///         2b. Handle apply <br/>
        ///         2c. Handle cancellation. <br/>
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns>inputDeps.</returns>
        public JobHandle HandleRoadEdgeUpdate(JobHandle inputDeps) {
            var logPrefix = "HandleRoadEdgeUpdate()";

            // ###
            // This first part of the code is about handling raycast hits and action presses.
            // It will update entity references and tool states.
            // ###
            if (GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                // Store results
                var previousHoveredEntity = m_HoveredEntity;
                m_HoveredEntity = entity;
                m_LastHitPosition = raycastHit.m_HitPosition;

                // Check for apply action initiation.
                var applyWasPressed = m_ApplyAction.WasPressedThisFrame() ||
                                     (m_ToolSystem.actionMode.IsEditor() && applyAction.WasPressedThisFrame());

                if (applyWasPressed) {
                    if (entity != m_ModeData.SelectedEdgeEntity &&
                        EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef) &&
                        EntityManager.TryGetComponent<Curve>(entity, out var curve) &&
                        EntityManager.TryGetComponent<EdgeGeometry>(entity, out var edgeGeo) &&
                        EntityManager.TryGetComponent<Composition>(entity, out var composition) &&
                        EntityManager.TryGetComponent<NetCompositionData>(composition.m_Edge, out var edgeNetCompData) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase)) {
                        // New Road..
                        m_Log.Debug($"{logPrefix} -- Selected entity: {entity}");

                        // Highlighting
                        SwapHighlitedEntities(m_ModeData.SelectedEdgeEntity, entity);

                        // Store results
                        m_ModeData.SelectedEdgeEntity = entity;
                        m_ModeData.SelectedCurve = curve;
                        m_ModeData.SelectedCurveGeo = edgeNetCompData;
                        m_ModeData.SelectedCurveEdgeGeo = edgeGeo;
                        m_ModeData.SelectedPrefabBase = prefabBase;
                    }
                } else if (previousHoveredEntity != m_HoveredEntity) {
                    // If we were previously hovering over an Edge that isn't the selected one, unhighlight it.
                    if (previousHoveredEntity != m_ModeData.SelectedEdgeEntity) {
                        ChangeHighlighting(previousHoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                    }

                    // Highlight the hovered entity
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.AddHighlight);
                }
            } else if (m_CancelAction.WasPressedThisFrame() ||
                (m_ModeData.SelectedEdgeEntity != Entity.Null && !EntityManager.Exists(m_ModeData.SelectedEdgeEntity))) {
                // Right click & we had something selected -> deselect and reset the tool.
                ChangeHighlighting(m_ModeData.SelectedEdgeEntity, ChangeObjectHighlightMode.RemoveHighlight);
                ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                m_ModeData.SelectedEdgeEntity = Entity.Null;
                m_HoveredEntity = Entity.Null;
                m_LastHitPosition = float3.zero;
            } else if (m_HoveredEntity != Entity.Null) {
                // No raycast hit, no action pressed, remove hover from any entity that was being hovered before.
                if (m_HoveredEntity != m_ModeData.SelectedEdgeEntity) {
                    ChangeHighlighting(m_HoveredEntity, ChangeObjectHighlightMode.RemoveHighlight);
                }

                m_HoveredEntity = Entity.Null;
                m_LastHitPosition = float3.zero;
            }

            // ###
            // This second part of the code handles the creation of preview entities.
            // ###
            if (m_ModeData.SelectedEdgeEntity != Entity.Null) {
                var terrainHeightData = m_TerrainSystem.GetHeightData();
                m_Points.Clear();

                // Demo data
                var rotation = 1;
                var width = m_SelectedParcelSize.x * DimensionConstants.CellSize;

                m_ModeData.CalculatePoints(
                    RoadEditorSpacing,
                    rotation,
                    RoadEditorOffset,
                    width,
                    m_Points,
                    ref terrainHeightData,
                    RoadEditorSides,
                    m_OverlayBuffer
                );

                m_Log.Debug($"Rendering {m_Points.Count} parcels");

                // Step along length and place preview objects.
                foreach (Transform transformData in m_Points) {
                    // m_OverlayBuffer.DrawCircle(UnityEngine.Color.white, transformData.m_Position, 3f);
                    var trs = ParcelUtils.GetTransformMatrix(transformData);
                    var pColor = UnityEngine.Color.red;

                    if (m_PlatterOverlaySystem.TryGetZoneColor(PreZoneType, out var color)) {
                        pColor = color;
                    }

                    m_Log.Debug($"Rendering parcel with {PreZoneType.m_Index} zone type");

                    DrawingUtils.DrawParcel(m_OverlayBuffer, pColor, SelectedParcelSize, trs);

                    //// Create entity.
                    // m_Log.Debug($"Creating temp parcel from enitity {SelectedEntity} transform {transformData.ToJSONString()}");
                    // CreateDefinitions(
                    //    _selectedEntity,
                    //    thisPoint.Position,
                    //    CurrentRotationMode == RotationMode.Random ? GetEffectiveRotation(thisPoint.Position) : thisPoint.Rotation,
                    //    CurrentSpacingMode == SpacingMode.FenceMode ? randomSeed : RandomizationEnabled ? GetRandomSeed(seedIndex++) : GetRandomSeed(0));
                }
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

        /// <summary>
        /// Creates temporary object definitions for previewing.
        /// </summary>
        /// <param name="objectPrefab">Object prefab entity.</param>
        /// <param name="position">Entity position.</param>
        /// <param name="rotation">Entity rotation.</param>
        /// <param name="randomSeed">Random seed to use.</param>
        private void CreateDefinitions(Entity objectPrefab, float3 position, quaternion rotation, RandomSeed randomSeed) {
            CreateDefinitions definitions = default;
            definitions.m_EditorMode = m_ToolSystem.actionMode.IsEditor();
            definitions.m_LefthandTraffic = m_CityConfigurationSystem.leftHandTraffic;
            definitions.m_ObjectPrefab = objectPrefab;
            definitions.m_Theme = m_CityConfigurationSystem.defaultTheme;
            definitions.m_RandomSeed = randomSeed;
            definitions.m_ControlPoint = new() {
                m_Position = position,
                m_Rotation = rotation
            };
            definitions.m_AttachmentPrefab = default;
            definitions.m_OwnerData = SystemAPI.GetComponentLookup<Owner>(true);
            definitions.m_TransformData = SystemAPI.GetComponentLookup<Transform>(true);
            definitions.m_AttachedData = SystemAPI.GetComponentLookup<Attached>(true);
            definitions.m_LocalTransformCacheData = SystemAPI.GetComponentLookup<LocalTransformCache>(true);
            definitions.m_ElevationData = SystemAPI.GetComponentLookup<Game.Objects.Elevation>(true);
            definitions.m_BuildingData = SystemAPI.GetComponentLookup<Building>(true);
            definitions.m_LotData = SystemAPI.GetComponentLookup<Game.Buildings.Lot>(true);
            definitions.m_EdgeData = SystemAPI.GetComponentLookup<Edge>(true);
            definitions.m_NodeData = SystemAPI.GetComponentLookup<Game.Net.Node>(true);
            definitions.m_CurveData = SystemAPI.GetComponentLookup<Curve>(true);
            definitions.m_NetElevationData = SystemAPI.GetComponentLookup<Game.Net.Elevation>(true);
            definitions.m_OrphanData = SystemAPI.GetComponentLookup<Orphan>(true);
            definitions.m_UpgradedData = SystemAPI.GetComponentLookup<Upgraded>(true);
            definitions.m_CompositionData = SystemAPI.GetComponentLookup<Composition>(true);
            definitions.m_AreaClearData = SystemAPI.GetComponentLookup<Clear>(true);
            definitions.m_AreaSpaceData = SystemAPI.GetComponentLookup<Space>(true);
            definitions.m_AreaLotData = SystemAPI.GetComponentLookup<Game.Areas.Lot>(true);
            definitions.m_EditorContainerData = SystemAPI.GetComponentLookup<Game.Tools.EditorContainer>(true);
            definitions.m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true);
            definitions.m_PrefabNetObjectData = SystemAPI.GetComponentLookup<NetObjectData>(true);
            definitions.m_PrefabBuildingData = SystemAPI.GetComponentLookup<BuildingData>(true);
            definitions.m_PrefabAssetStampData = SystemAPI.GetComponentLookup<AssetStampData>(true);
            definitions.m_PrefabBuildingExtensionData = SystemAPI.GetComponentLookup<BuildingExtensionData>(true);
            definitions.m_PrefabSpawnableObjectData = SystemAPI.GetComponentLookup<SpawnableObjectData>(true);
            definitions.m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(true);
            definitions.m_PrefabPlaceableObjectData = SystemAPI.GetComponentLookup<PlaceableObjectData>(true);
            definitions.m_PrefabAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>(true);
            definitions.m_PrefabBuildingTerraformData = SystemAPI.GetComponentLookup<BuildingTerraformData>(true);
            definitions.m_PrefabCreatureSpawnData = SystemAPI.GetComponentLookup<CreatureSpawnData>(true);
            definitions.m_PlaceholderBuildingData = SystemAPI.GetComponentLookup<PlaceholderBuildingData>(true);
            definitions.m_PrefabNetGeometryData = SystemAPI.GetComponentLookup<NetGeometryData>(true);
            definitions.m_PrefabCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>(true);
            definitions.m_SubObjects = SystemAPI.GetBufferLookup<Game.Objects.SubObject>(true);
            definitions.m_CachedNodes = SystemAPI.GetBufferLookup<LocalNodeCache>(true);
            definitions.m_InstalledUpgrades = SystemAPI.GetBufferLookup<InstalledUpgrade>(true);
            definitions.m_SubNets = SystemAPI.GetBufferLookup<Game.Net.SubNet>(true);
            definitions.m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>(true);
            definitions.m_SubAreas = SystemAPI.GetBufferLookup<Game.Areas.SubArea>(true);
            definitions.m_AreaNodes = SystemAPI.GetBufferLookup<Game.Areas.Node>(true);
            definitions.m_AreaTriangles = SystemAPI.GetBufferLookup<Triangle>(true);
            definitions.m_PrefabSubObjects = SystemAPI.GetBufferLookup<Game.Prefabs.SubObject>(true);
            definitions.m_PrefabSubNets = SystemAPI.GetBufferLookup<Game.Prefabs.SubNet>(true);
            definitions.m_PrefabSubLanes = SystemAPI.GetBufferLookup<Game.Prefabs.SubLane>(true);
            definitions.m_PrefabSubAreas = SystemAPI.GetBufferLookup<Game.Prefabs.SubArea>(true);
            definitions.m_PrefabSubAreaNodes = SystemAPI.GetBufferLookup<SubAreaNode>(true);
            definitions.m_PrefabPlaceholderElements = SystemAPI.GetBufferLookup<PlaceholderObjectElement>(true);
            definitions.m_PrefabRequirementElements = SystemAPI.GetBufferLookup<ObjectRequirementElement>(true);
            definitions.m_PrefabServiceUpgradeBuilding = SystemAPI.GetBufferLookup<ServiceUpgradeBuilding>(true);
            definitions.m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var _);
            definitions.m_TerrainHeightData = m_TerrainSystem.GetHeightData();
            definitions.m_CommandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            definitions.Execute();
        }
    }
}
