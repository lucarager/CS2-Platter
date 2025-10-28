// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Audio;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Platter.Components;
using Platter.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Platter.Systems {
    public partial class P_TestToolSystem : ToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "PlatterTestTool";

        private PrefixedLogger      m_Log;
        private PrefabBase          m_SelectedPrefab;
        private Entity              m_PlacePrefabEntity;
        private Transform           m_PlaceTransform;
        private ushort              m_PlacePrezoneIndex;
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
            m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- PlaceObjectPrefab");
            PlaceObjectPrefab(m_PlacePrefabEntity, m_PlaceTransform);

            if (m_TempQuery.IsEmpty) {
                return inputDeps;
            }

            m_PlacedEntity = m_TempQuery.ToEntityArray(Allocator.Temp)[0];
            PrepareParcelForApply(m_PlacedEntity, m_PlacePrezoneIndex);

            m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- Apply");

            // Apply
            applyMode           = ApplyMode.Apply;
            m_PlacePrefabEntity = Entity.Null;
            m_PlaceTransform    = default;
            m_PlacePrezoneIndex = 0;

            return inputDeps;
        }

        public override PrefabBase GetPrefab() {
            return m_SelectedPrefab;
        }

        public async Task<Entity> Plop(Entity prefab, Transform transform, ushort prezoneIndex) {
            while (m_PlacePrefabEntity != Entity.Null) {
                await Task.Delay(10);
            }

            m_PlacePrefabEntity = prefab;
            m_PlaceTransform    = transform;
            m_PlacePrezoneIndex = prezoneIndex;
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

        public void PlaceObjectPrefab(Entity prefab, Transform transform) {
            var ecb    = new EntityCommandBuffer(Allocator.TempJob);
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

        private void PlaceEdge() {
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

        public void PrepareParcelForApply(Entity entity, ushort prezoneIndex) {
            var ecb    = new EntityCommandBuffer(Allocator.TempJob);

            var parcel = EntityManager.GetComponentData<Parcel>(entity);
            parcel.m_PreZoneType.m_Index = prezoneIndex;
            ecb.SetComponent<Parcel>(entity, parcel);

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}

