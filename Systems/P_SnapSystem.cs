// <copyright file="P_SnapSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Collections;
    using Colossal.Mathematics;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Platter.Utils;
    using System;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using static Game.Rendering.OverlayRenderSystem;
    using static Platter.Utils.DrawingUtils;

    /// <summary>
    /// Overlay Rendering System.
    /// <todo>Add culling and burst</todo>
    /// </summary>
    public partial class P_SnapSystem : GameSystemBase {
        public static float MAX_SNAP_DISTANCE = 16f;
        public static float DEFAULT_SNAP_DISTANCE = 4f;

        // Props
        public SnapMode CurrentSnapMode {
            get => m_SnapMode;

            set => m_SnapMode = value;
        }

        public float CurrentSnapOffset {
            get => m_SnapOffset;

            set => m_SnapOffset = value;
        }

        // Logger
        private PrefixedLogger m_Log;

        // Systems
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private TerrainSystem m_TerrainSystem;

        // Data
        private EntityQuery m_Query;
        private SnapMode m_SnapMode;
        private float m_SnapOffset;

        [Flags]
        public enum SnapMode : uint {
            None = 0u,
            RoadSide = 1u,
            All = uint.MaxValue,
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Systems
            m_ZoneSearchSystem = base.World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_SnapSystem));
            m_Log.Debug($"OnCreate()");

            // Query
            m_Query = SystemAPI.QueryBuilder()
                .WithAllRW<Game.Tools.ObjectDefinition>()
                .WithAll<CreationDefinition, Updated>()
                .WithNone<Deleted, Overridden>()
                .Build();

            // Data
            m_SnapOffset = P_SnapSystem.DEFAULT_SNAP_DISTANCE;
            m_SnapMode = SnapMode.RoadSide;

            RequireForUpdate(m_Query);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Exit early on certain conditions
            if (m_Query.IsEmptyIgnoreFilter ||
                m_ToolSystem.activeTool is not ObjectToolSystem ||
                m_ObjectToolSystem.prefab is not ParcelPrefab) {
                return;
            }

            // Exit on disabled snap
            if (!m_SnapMode.HasFlag(SnapMode.RoadSide)) {
                return;
            }

            // Grab control points from ObjectTool
            var controlPoints = m_ObjectToolSystem.GetControlPoints(out var deps);
            base.Dependency = JobHandle.CombineDependencies(base.Dependency, deps);

            // If none, exit
            if (controlPoints.Length == 0) {
                return;
            }

            // Schedule our snapping job
            var parcelSnapJobHandle = new ParcelSnapJob (
                tree: m_ZoneSearchSystem.GetSearchTree(true, out var zoneTreeJobHandle),
                controlPoints: controlPoints,
                objectDefinitionTypeHandle: SystemAPI.GetComponentTypeHandle<ObjectDefinition>(false),
                creationDefinitionTypeHandle: SystemAPI.GetComponentTypeHandle<CreationDefinition>(true),
                blockComponentLookup: SystemAPI.GetComponentLookup<Block>(true),
                parcelDataComponentLookup: SystemAPI.GetComponentLookup<ParcelData>(true),
                terrainHeightData: m_TerrainSystem.GetHeightData(false),
                snapOffset: m_SnapOffset,
                entityTypeHandle: SystemAPI.GetEntityTypeHandle()
            ).ScheduleParallel(
                m_Query,
                JobHandle.CombineDependencies(base.Dependency, zoneTreeJobHandle)
            );

            m_ZoneSearchSystem.AddSearchTreeReader(parcelSnapJobHandle);

            // Register deps
            base.Dependency = JobHandle.CombineDependencies(base.Dependency, parcelSnapJobHandle);
        }

        private struct Rotation {
            public quaternion m_Rotation;
            public bool m_IsAligned;
            public bool m_IsSnapped;
        }

        private struct ParcelSnapJob : IJobChunk {
            [ReadOnly]
            public NativeQuadTree<Entity, Bounds2> m_Tree;

            [ReadOnly]
            public TerrainHeightData m_TerrainHeightData;

            [ReadOnly]
            public NativeList<ControlPoint> m_ControlPoints;

            [ReadOnly]
            public ComponentTypeHandle<CreationDefinition> m_CreationDefinitionTypeHandle;

            [ReadOnly]
            public ComponentLookup<Block> m_BlockComponentLookup;

            [ReadOnly]
            public ComponentLookup<ParcelData> m_ParcelDataComponentLookup;

            [ReadOnly]
            public float m_SnapOffset;

            [ReadOnly]
            public EntityTypeHandle m_EntityTypeHandle;

            public ComponentTypeHandle<ObjectDefinition> m_ObjectDefinitionTypeHandle;

            public ParcelSnapJob(
                NativeQuadTree<Entity, Bounds2> tree,
                TerrainHeightData terrainHeightData,
                NativeList<ControlPoint> controlPoints,
                ComponentTypeHandle<CreationDefinition> creationDefinitionTypeHandle,
                ComponentLookup<Block> blockComponentLookup,
                ComponentLookup<ParcelData> parcelDataComponentLookup,
                float snapOffset,
                EntityTypeHandle entityTypeHandle,
                ComponentTypeHandle<ObjectDefinition> objectDefinitionTypeHandle) {
                m_Tree = tree;
                m_TerrainHeightData = terrainHeightData;
                m_ControlPoints = controlPoints;
                m_CreationDefinitionTypeHandle = creationDefinitionTypeHandle;
                m_BlockComponentLookup = blockComponentLookup;
                m_ParcelDataComponentLookup = parcelDataComponentLookup;
                m_SnapOffset = snapOffset;
                m_EntityTypeHandle = entityTypeHandle;
                m_ObjectDefinitionTypeHandle = objectDefinitionTypeHandle;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var entityArray = chunk.GetNativeArray(m_EntityTypeHandle);
                var objectDefinitionArray = chunk.GetNativeArray<ObjectDefinition>(ref m_ObjectDefinitionTypeHandle);
                var creationDefinitionArray = chunk.GetNativeArray<CreationDefinition>(ref m_CreationDefinitionTypeHandle);

                for (var i = 0; i < entityArray.Length; i++) {
                    var entity = entityArray[i];
                    var creationDefinition = creationDefinitionArray[i];
                    var objectDefinition = objectDefinitionArray[i];

                    // Get some data
                    var parcelData = m_ParcelDataComponentLookup[creationDefinition.m_Prefab];

                    // Calculate geometry
                    var searchRadius = (parcelData.m_LotSize.y * 4f) + 16f;
                    var minValue = float.MinValue;
                    var controlPoint = m_ControlPoints[0];
                    var bounds = new Bounds3(
                        controlPoint.m_Position - searchRadius,
                        controlPoint.m_Position + searchRadius
                    );

                    // At minimum, the distance to snap must be
                    var minDistance = (math.cmin(parcelData.m_LotSize) * 4f) + 16f;

                    // Default to our control point as a start
                    var bestSnapPosition = controlPoint;

                    // Iterate over zones to find a snapping point
                    var iterator = new BlockSnapIterator(
                        controlPoint: controlPoint,
                        bestSnapPosition: bestSnapPosition,
                        bounds: bounds.xz,
                        blockComponentLookup: m_BlockComponentLookup,
                        bestDistance: minDistance,
                        lotSize: parcelData.m_LotSize,
                        snapOffset: m_SnapOffset
                    );
                    m_Tree.Iterate<BlockSnapIterator>(ref iterator, 0);

                    // Retrieve best found point
                    bestSnapPosition = iterator.m_BestSnapPosition;
                    var hasSnap = !controlPoint.m_Position.Equals(bestSnapPosition.m_Position);

                    // Height Calc
                    CalculateHeight(ref bestSnapPosition, parcelData, minValue);

                    // If we found a snapping point, modify the object definition
                    if (hasSnap) {
                        objectDefinition.m_Position = bestSnapPosition.m_Position;
                        objectDefinition.m_LocalPosition = bestSnapPosition.m_Position;
                        objectDefinition.m_Rotation = bestSnapPosition.m_Rotation;
                        objectDefinition.m_LocalRotation = bestSnapPosition.m_Rotation;
                        objectDefinitionArray[i] = objectDefinition;
                    }
                }
            }

            private void CalculateHeight(ref ControlPoint controlPoint, ParcelData parcelData, float waterSurfaceHeight) {
                var parcelFrontPosition = BuildingUtils.CalculateFrontPosition(new Game.Objects.Transform(controlPoint.m_Position, controlPoint.m_Rotation), parcelData.m_LotSize.y);
                var targetHeight = TerrainUtils.SampleHeight(ref m_TerrainHeightData, parcelFrontPosition);
                controlPoint.m_Position.y = targetHeight;
            }
        }

        /// <summary>
        /// QuadTree iterator.
        /// </summary>
        public struct BlockSnapIterator : INativeQuadTreeIterator<Entity, Bounds2>, IUnsafeQuadTreeIterator<Entity, Bounds2> {
            public ComponentLookup<Block> m_BlockComponentLookup;
            public ControlPoint m_ControlPoint;
            public int2 m_LotSize;
            public Bounds2 m_Bounds;
            public ControlPoint m_BestSnapPosition;
            public float m_BestDistance;
            public float m_SnapOffset;

            public BlockSnapIterator(ComponentLookup<Block> blockComponentLookup, ControlPoint controlPoint, int2 lotSize, Bounds2 bounds, ControlPoint bestSnapPosition, float bestDistance, float snapOffset) {
                m_BlockComponentLookup = blockComponentLookup;
                m_ControlPoint = controlPoint;
                m_LotSize = lotSize;
                m_Bounds = bounds;
                m_BestSnapPosition = bestSnapPosition;
                m_BestDistance = bestDistance;
                m_SnapOffset = snapOffset;
            }

            /// <summary>
            /// Tests whether XZ bounds intersect.
            /// </summary>
            /// <param name="bounds">Quad tree bounds (XZ) for testing.</param>
            /// <returns><c>true</c> if bounds intersect, <c>false</c> otherwise.</returns>
            public bool Intersect(Bounds2 bounds) {
                return MathUtils.Intersect(bounds, m_Bounds);
            }

            /// <summary>
            /// BlockSnapIterator.
            /// </summary>
            /// <param name="bounds">Bounds blockCorners tree to check for intersection.</param>
            /// <param name="blockEntity">Entity to enqueue if bounds intersect.</param>
            public void Iterate(Bounds2 bounds, Entity blockEntity) {
                // Early exit if bounds don't intersect
                if (!MathUtils.Intersect(bounds, m_Bounds)) {
                    return;
                }

                // Get the block's geometry
                var block = m_BlockComponentLookup[blockEntity];
                var blockCorners = ZoneUtils.CalculateCorners(block);
                var blockFrontEdge = new Line2.Segment(blockCorners.a, blockCorners.b);


                // Create a search line at the cursor position
                var searchLine = new Line2.Segment(m_ControlPoint.m_HitPosition.xz, m_ControlPoint.m_HitPosition.xz);

                // Extend the search line based on lot depth vs width difference
                var lotDepthDifference = math.max(0f, m_LotSize.y - m_LotSize.x) * 4f;
                var extensionVector = lotDepthDifference;
                searchLine.a -= extensionVector;
                searchLine.b += extensionVector;

                // Calculate distance between the block's front edge and our search line
                var distanceToBlock = MathUtils.Distance(blockFrontEdge, searchLine, out var intersectionParams);

                // If distance is exactly 0 (overlapping),
                // applies a bonus based on how centered the overlap is (prefers center alignment)
                if (distanceToBlock == 0f) {
                    var centeredness = 0.5f - math.abs(intersectionParams.y - 0.5f);
                    distanceToBlock -= centeredness;
                }

                // If distance exceeds our "best", exit
                if (distanceToBlock >= m_BestDistance) {
                    return;
                }

                m_BestDistance = distanceToBlock;

                // Calculate offset from block center to cursor
                var cursorOffsetFromBlock = m_ControlPoint.m_HitPosition.xz - block.m_Position.xz;

                // Get perpendicular direction for lateral calculations
                var blockForwardDirection = block.m_Direction;
                var blockLeftDirection = MathUtils.Left(block.m_Direction);

                // Calculate depths (multiplied by 4 for grid units)
                var blockDepth = block.m_Size.y * 4f;
                var parcelLotDepth = m_LotSize.y * 4f;

                // Project cursor offset onto block's forward and lateral axes
                var forwardOffset = math.dot(blockForwardDirection, cursorOffsetFromBlock);
                var lateralOffset = math.dot(blockLeftDirection, cursorOffsetFromBlock);

                // Snap lateral position to 8-unit grid, accounting for odd/even width differences
                var hasDifferentParity = ((block.m_Size.x ^ m_LotSize.x) & 1) != 0;
                var parityOffset = hasDifferentParity ? 0.5f : 0f;
                lateralOffset -= (math.round((lateralOffset / 4f) - parityOffset) + parityOffset) * 4f;

                // Build the snapped position
                m_BestSnapPosition = m_ControlPoint;
                m_BestSnapPosition.m_Position = m_ControlPoint.m_HitPosition;

                // Align new lot behind the existing block's front edge
                var depthAlignment = blockDepth - forwardOffset - m_SnapOffset;
                m_BestSnapPosition.m_Position.xz += blockForwardDirection * depthAlignment;

                // Apply lateral grid snapping
                m_BestSnapPosition.m_Position.xz -= blockLeftDirection * lateralOffset;

                // Set direction and rotation to match the block
                m_BestSnapPosition.m_Direction = block.m_Direction;
                m_BestSnapPosition.m_Rotation = ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);

                // Calculate snap priority
                m_BestSnapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                    0f,
                    1f,
                    0f,
                    m_ControlPoint.m_HitPosition * 0.5f,
                    m_BestSnapPosition.m_Position * 0.5f,
                    m_BestSnapPosition.m_Direction
                );

                // Cache block
                m_BestSnapPosition.m_OriginalEntity = blockEntity;
            }
        }
    }
}
