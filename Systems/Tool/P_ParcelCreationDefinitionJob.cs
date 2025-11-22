// <copyright file="P_ParcelCreationDefinitionJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Game.Common;
using Game.Objects;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Platter.Systems {
    internal static class P_ParcelCreationDefinitionJob {
        internal struct ParcelCreationDefinitionJob : IJob {
            [ReadOnly] public required Entity                                  m_ObjectPrefab;
            [ReadOnly] public required RandomSeed                              m_RandomSeed;
            [ReadOnly] public required ControlPoint                            m_ControlPoint;
            [ReadOnly] public required ComponentLookup<Transform>              m_TransformLookup;
            [ReadOnly] public required ComponentLookup<Game.Objects.Elevation> m_ElevationLookup;
            public required            EntityCommandBuffer                     m_CommandBuffer;

            public void Execute() {
                var controlPoint = m_ControlPoint;
                var entity       = Entity.Null;
                var transform    = m_TransformLookup[entity];
                m_ElevationLookup.TryGetComponent(entity, out var elevation);

                var ownerDefinition = default(OwnerDefinition);
                var random          = m_RandomSeed.GetRandom(0);
                var newEntity       = m_CommandBuffer.CreateEntity();

                CreationDefinition creationDef = default;
                creationDef.m_Prefab     = m_ObjectPrefab;
                creationDef.m_SubPrefab  = Entity.Null;
                creationDef.m_Owner      = Entity.Null;
                creationDef.m_Original   = Entity.Null;
                creationDef.m_RandomSeed = random.NextInt();

                ObjectDefinition objectDef = default;
                objectDef.m_ParentMesh     = -1;
                objectDef.m_Position       = transform.m_Position;
                objectDef.m_Rotation       = transform.m_Rotation;
                objectDef.m_Probability    = 100;
                objectDef.m_PrefabSubIndex = -1;
                objectDef.m_Scale          = 1f;
                objectDef.m_Intensity      = 1f;
                objectDef.m_Elevation      = controlPoint.m_Elevation;
                objectDef.m_LocalPosition  = transform.m_Position;
                objectDef.m_LocalRotation  = transform.m_Rotation;

                ownerDefinition.m_Prefab   = m_ObjectPrefab;
                ownerDefinition.m_Position = objectDef.m_Position;
                ownerDefinition.m_Rotation = objectDef.m_Rotation;

                m_CommandBuffer.AddComponent(newEntity, creationDef);
                m_CommandBuffer.AddComponent(newEntity, objectDef);
                m_CommandBuffer.AddComponent(newEntity, default(Updated));
            }
        }
    }
}