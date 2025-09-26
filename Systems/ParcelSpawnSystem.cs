using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Platter.Components;
using Platter.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

namespace Platter.Systems {
    public partial class ParcelSpawnSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_EnabledQuery;
        private EntityQuery m_DisabledQuery;

        // Barriers & Buffers
        private ModificationBarrier3 m_ModificationBarrier;
        private EntityCommandBuffer m_CommandBuffer;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(ParcelSpawnSystem));
            m_Log.Debug($"OnCreate()");

            // Retriefve Systems
            m_ModificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier3>();

            // Queries
            m_EnabledQuery = GetEntityQuery(new EntityQueryDesc[]
            {
                new () {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Parcel>(),
                        ComponentType.ReadOnly<ParcelSpawnable>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>()
                    }
                }
            });

            m_DisabledQuery = GetEntityQuery(new EntityQueryDesc[]
            {
                new () {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Parcel>(),
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<ParcelSpawnable>(),
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>()
                    }
                }
            });
        }

        protected override void OnUpdate() {
        }

        // Todo convert to job for perf
        public void UpdateSpawning(bool allowSpawn = true) {
            m_Log.Debug($"UpdateSpawning(allowSpawn={allowSpawn})");

            var query = allowSpawn ? m_DisabledQuery : m_EnabledQuery;

            if (allowSpawn) {
                EntityManager.AddComponent<ParcelSpawnable>(query);
            } else {
                EntityManager.RemoveComponent<ParcelSpawnable>(query);
            }
        }
    }
}
