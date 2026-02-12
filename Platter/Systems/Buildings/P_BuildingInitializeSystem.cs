// <copyright file="P_BuildingInitializeSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Components;
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Tools;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for adding the GrowableBuilding and LinkedParcel components to buildings.
    /// </summary>
    public partial class P_BuildingInitializeSystem : PlatterGameSystemBase {
        private EntityQuery m_SpawnedBuildingQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_SpawnedBuildingQuery = SystemAPI.QueryBuilder()
                                              .WithAll<Building>()
                                              .WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>()
                                              .WithNone<Temp, Deleted, Signature, LinkedParcel, GrowableBuilding>()
                                              .Build();

            RequireForUpdate(m_SpawnedBuildingQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate() -- Initializing {m_SpawnedBuildingQuery.CalculateEntityCount()} spawned buildings");
            EntityManager.AddComponent(m_SpawnedBuildingQuery, new ComponentTypeSet(typeof(GrowableBuilding), typeof(LinkedParcel), typeof(TransformUpdated)));
        }
    }
}