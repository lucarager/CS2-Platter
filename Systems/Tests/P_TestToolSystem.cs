// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System;
    using System.Threading.Tasks;
    using Colossal.Serialization.Entities;
    using Game.Common;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Colossal.Mathematics;
    using Unity.Mathematics;

    public partial class P_TestToolSystem : ToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "PlatterTestTool";

        public enum PrefabType {
            Object,
            Edge,
        }

        private PrefixedLogger      m_Log;
        private PrefabBase          m_SelectedPrefab;
        private Entity              m_PlacePrefabEntity;
        private PrefabType          m_PlacePrefabType;
        private Transform           m_PlaceTransform1;
        private Transform           m_PlaceTransform2;
        private Entity              m_PlacedEntity;
        private EntityQuery         m_TempQuery;
        private EntityQuery         m_CreatedEntityQuery;
        private ToolOutputBarrier   m_ToolOutputBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(P_TestToolSystem));

            m_TempQuery = SystemAPI.QueryBuilder().WithAll<Temp>().Build();
            m_CreatedEntityQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Created>()
                                            .Build();

            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();

            var toolList        = World.GetOrCreateSystemManaged<ToolSystem>().tools;
            ToolBaseSystem thisSystem      = null;
            var            objectToolIndex = 0;

            for (var i = 0; i < toolList.Count; i++) {
                var tool = toolList[i];
                if (tool == this) {
                    thisSystem = tool;
                }

                if (tool.toolID.Equals("Object Tool")) {
                    objectToolIndex = i;
                }
            }

            // Remove existing tool reference.
            if (thisSystem is not null) {
                toolList.Remove(this);
            }

            toolList.Insert(objectToolIndex - 1, this);
        }

        protected override void OnGameLoaded(Context serializationContext) {
            base.OnGameLoaded(serializationContext);
            m_Log.Debug($"OnGameLoaded(Context serializationContext)");
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            m_Log.Debug($"OnDestroy()");
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();
            m_Log.Debug($"OnStartRunning()");
            applyMode = ApplyMode.Clear;
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();
            m_Log.Debug($"OnStopRunning()");
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            m_CommandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            applyMode       = ApplyMode.Clear;

            if (m_PlacePrefabEntity == Entity.Null) {
                m_SelectedPrefab = null;

                return inputDeps;
            }

            applyMode = ApplyMode.None;


            switch (m_PlacePrefabType) {
                case PrefabType.Edge:
                    m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- PlaceEdge");
                    PlaceEdge(m_PlacePrefabEntity, m_PlaceTransform1, m_PlaceTransform2);
                    break;
                case PrefabType.Object:
                    m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- PlaceObject");
                    PlaceObject(m_PlacePrefabEntity, m_PlaceTransform1);
                    break;
            }

            if (m_TempQuery.IsEmpty) {
                return inputDeps;
            }

            m_PlacedEntity = m_TempQuery.ToEntityArray(Allocator.Temp)[0];
            m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- Apply");

            // Apply
            applyMode           = ApplyMode.Apply;
            m_PlacePrefabEntity = Entity.Null;
            m_PlaceTransform1   = default;
            m_PlaceTransform2   = default;

            return inputDeps;
        }

        public override PrefabBase GetPrefab() {
            return m_SelectedPrefab;
        }

        public async Task<Entity> PlopObject(Entity prefab, Transform transform) {
            while (m_PlacePrefabEntity != Entity.Null) {
                await Task.Delay(10);
            }

            m_PlacePrefabEntity = prefab;
            m_PlaceTransform1   = transform;
            m_PlacePrefabType   = PrefabType.Object;

            var maxTries = 10;
            var tries    = 0;
            var entity   = Entity.Null;

            while (entity == Entity.Null || tries++ <= maxTries) {
                entity = m_PlacedEntity;
                await Task.Delay(10);
            }
            
            if (tries == maxTries) {
                throw new Exception("Could not find created entity");
            }

            m_PlacedEntity = Entity.Null;

            return entity;
        }

        public async Task<Entity> PlopEdge(Entity prefab, Transform startNodePosition, Transform endNodePosition) {
            while (m_PlacePrefabEntity != Entity.Null) {
                await Task.Delay(10);
            }

            m_PlacePrefabEntity = prefab;
            m_PlaceTransform1   = startNodePosition;
            m_PlaceTransform2   = endNodePosition;
            m_PlacePrefabType   = PrefabType.Edge;

            var maxTries = 10;
            var tries    = 0;
            var entity   = Entity.Null;

            while (entity == Entity.Null || tries++ <= maxTries) {
                entity = m_PlacedEntity;
                await Task.Delay(10);
            }

            if (tries == maxTries) {
                throw new Exception("Could not find created entity");
            }

            m_PlacedEntity = Entity.Null;

            return entity;
        }

        public void Enable() {
            if (!PlatterMod.Instance.IsTestMode) {
                return;
            }

            m_Log.Debug($"RequestEnable()");
            m_ToolSystem.activeTool = this;
        }

        public override bool TrySetPrefab(PrefabBase prefab) {
            if (!PlatterMod.Instance.IsTestMode) {
                return false;
            }

            m_Log.Debug($"TrySetPrefab({prefab})");

            if (m_ToolSystem.activeTool != this) {
                return false;
            }

            m_SelectedPrefab = prefab;
            return true;
        }

        public void PlaceObject(Entity prefab, Transform transform) {
            var ecb    = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new CreationDefinition() {
                m_Prefab    = prefab,
                m_SubPrefab = Entity.Null,
                m_Owner     = Entity.Null,
                m_Original  = Entity.Null,
            });

            ecb.AddComponent(entity, new ObjectDefinition() {
                m_ParentMesh     = -1,
                m_Position       = transform.m_Position,
                m_Rotation       = transform.m_Rotation,
                m_Probability    = 100,
                m_PrefabSubIndex = -1,
                m_Scale          = 1f,
                m_Intensity      = 1f,
                m_Elevation      = transform.m_Position.y,
                m_LocalRotation  = transform.m_Rotation,
            });

            ecb.AddComponent(entity, default(OwnerDefinition));

            ecb.AddComponent(entity, default(Updated));

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void PlaceEdge(Entity prefab, Transform startNodePosition, Transform endNodePosition) {
            var ecb    = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();

            ecb.AddComponent(entity, new CreationDefinition() {
                m_Prefab    = prefab,
                m_SubPrefab = Entity.Null,
                m_Owner     = Entity.Null,
                m_Original  = Entity.Null,
            });

            ecb.AddComponent(entity, new NetCourse() {
                m_StartPosition = new CoursePos() {
                    m_Entity        = Entity.Null,
                    m_Position      = startNodePosition.m_Position,
                    m_Rotation      = startNodePosition.m_Rotation,
                    m_Elevation     = startNodePosition.m_Position.y,
                    m_CourseDelta   = 0,
                    m_SplitPosition = 0,
                    m_Flags         = CoursePosFlags.IsFirst | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                    m_ParentMesh    = -1,
                },
                m_EndPosition = new CoursePos() {
                    m_Entity        = Entity.Null,
                    m_Position      = endNodePosition.m_Position,
                    m_Rotation      = endNodePosition.m_Rotation,
                    m_Elevation     = endNodePosition.m_Position.y,
                    m_CourseDelta   = 1,
                    m_SplitPosition = 0,
                    m_Flags         = CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.IsLeft,
                    m_ParentMesh    = -1,
                },
                m_Curve      = new Bezier4x3(startNodePosition.m_Position, startNodePosition.m_Position, endNodePosition.m_Position, endNodePosition.m_Position),
                m_Elevation  = new float2(0, 0),
                m_Length     = math.distance(startNodePosition.m_Position, endNodePosition.m_Position),
                m_FixedIndex = -1,
            });

            ecb.AddComponent(entity, default(OwnerDefinition));

            ecb.AddComponent(entity, default(Updated));

            ecb.Playback(EntityManager);
            ecb.Dispose();

            //-course  { Game.Tools.NetCourse}
            //Game.Tools.NetCourse
            //-m_Curve { Colossal.Mathematics.Bezier4x3}
            //Colossal.Mathematics.Bezier4x3
            //+ x   { Colossal.Mathematics.Bezier4x1}
            //Colossal.Mathematics.Bezier4x1
            //+ xy  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ xz  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ y   { Colossal.Mathematics.Bezier4x1}
            //Colossal.Mathematics.Bezier4x1
            //+ yx  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ yz  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ z   { Colossal.Mathematics.Bezier4x1}
            //Colossal.Mathematics.Bezier4x1
            //+ zx  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ zy  { Colossal.Mathematics.Bezier4x2}
            //Colossal.Mathematics.Bezier4x2
            //+ a   { float3(751.603f, 884.8506f, 229.545f)}
            //Unity.Mathematics.float3
            //+ b   { float3(772.2596f, 883.812f, 257.3853f)}
            //Unity.Mathematics.float3
            //+ c   { float3(792.9163f, 882.8702f, 285.2256f)}
            //Unity.Mathematics.float3
            //+ d   { float3(813.5729f, 882.014f, 313.0659f)}
            //Unity.Mathematics.float3
            //- m_Elevation { float2(0f, 0f)}
            //Unity.Mathematics.float2
            //x   0   float
            //y   0   float
            //+Raw View
            //- m_EndPosition   { Game.Tools.CoursePos}
            //Game.Tools.CoursePos
            //m_CourseDelta   1   float
            //+m_Elevation { float2(0f, 0f)}
            //Unity.Mathematics.float2
            //+ m_Entity    "Entity.Null"   Unity.Entities.Entity
            //m_Flags IsLast | IsRight | IsLeft   Game.Tools.CoursePosFlags

            //m_ParentMesh - 1  int
            //+m_Position  { float3(813.5729f, 882.014f, 313.0659f)}
            //Unity.Mathematics.float3
            //+ m_Rotation  { quaternion(0f, 0.3137796f, 0f, 0.9494959f)}
            //Unity.Mathematics.quaternion
            //m_SplitPosition 0   float

            //m_FixedIndex - 1  int
            //m_Length    104.03881   float
            //-m_StartPosition { Game.Tools.CoursePos}
            //Game.Tools.CoursePos
            //m_CourseDelta   0   float
            //+m_Elevation { float2(0f, 0f)}
            //Unity.Mathematics.float2
            //+ m_Entity    "Entity.Null"   Unity.Entities.Entity
            //m_Flags IsFirst | IsRight | IsLeft  Game.Tools.CoursePosFlags

            //m_ParentMesh - 1  int
            //+m_Position  { float3(751.603f, 884.8506f, 229.545f)}
            //Unity.Mathematics.float3
            //+ m_Rotation  { quaternion(0f, 0.3137796f, 0f, 0.9494959f)}
            //Unity.Mathematics.quaternion
            //m_SplitPosition 0   float

            //-definitionData  { Game.Tools.CreationDefinition}
            //Game.Tools.CreationDefinition
            //+ m_Attached  "Entity.Null"   Unity.Entities.Entity
            //m_Flags SubElevation Game.Tools.CreationFlags
            //+ m_Original  "Entity.Null"   Unity.Entities.Entity
            //+ m_Owner "Entity.Null"   Unity.Entities.Entity
            //+ m_Prefab    "'' Entity(15996:1) Game"   Unity.Entities.Entity
            //m_RandomSeed    1_344_631_594   int
            //+m_SubPrefab "Entity.Null"   Unity.Entities.Entity

            //-ownerData   { Game.Tools.OwnerDefinition}
            //Game.Tools.OwnerDefinition
            //+ m_Position  { float3(0f, 0f, 0f)}
            //Unity.Mathematics.float3
            //+ m_Prefab    "Entity.Null"   Unity.Entities.Entity
            //+ m_Rotation  { quaternion(0f, 0f, 0f, 0f)}
            //Unity.Mathematics.quaternion

        }
    }
}

