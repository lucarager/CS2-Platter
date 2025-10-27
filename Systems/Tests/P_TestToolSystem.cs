// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
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
using Platter.Utils;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Platter.Systems {
    public partial class P_TestToolSystem : ToolBaseSystem {
        /// <inheritdoc/>
        public override string toolID => "PlatterTestTool";

        private PrefixedLogger m_Log;
        private PrefabBase     m_SelectedPrefab;
        private Entity      m_PlacePrefabEntity;
        private Transform   m_PlaceTransform;
        private EntityQuery m_TempQuery;
        private ToolOutputBarrier m_ToolOutputBarrier;
        private EntityCommandBuffer  m_CommandBuffer;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(P_TestToolSystem));
            m_TempQuery = SystemAPI.QueryBuilder().WithAll<Temp>().Build();
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

            if (m_PlacePrefabEntity != Entity.Null) {
                applyMode = ApplyMode.None;
                m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- PlaceObjectPrefab");
                PlaceObjectPrefab(m_PlacePrefabEntity, m_PlaceTransform);
            }

            if (m_TempQuery.IsEmpty) {
                return inputDeps;
            }

            m_Log.Debug($"OnUpdate(JobHandle inputDeps) -- Apply");

            // Apply
            applyMode           = ApplyMode.Apply;
            m_PlacePrefabEntity = Entity.Null;
            m_PlaceTransform    = default;

            return inputDeps;
        }

        public override PrefabBase GetPrefab() {
            return m_SelectedPrefab;
        }

        public void Place(Entity prefab, Transform transform) {
            m_PlacePrefabEntity = prefab;
            m_PlaceTransform    = transform;
        }

        public void Enable() {
            m_Log.Debug($"RequestEnable()");
            m_ToolSystem.activeTool = this;
        }

        public override bool TrySetPrefab(PrefabBase prefab) {
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
    }
}

