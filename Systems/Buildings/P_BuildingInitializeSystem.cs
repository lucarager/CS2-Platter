﻿// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Buildings;
    using Game.Common;
    using Game.Objects;
    using Game.Tools;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// System responsible for adding the GrowableBuilding and LinkedParcel components to buildings.
    /// </summary>
    public partial class P_BuildingInitializeSystem : GameSystemBase {
        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_SpawnedBuildingQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingInitializeSystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_SpawnedBuildingQuery = SystemAPI.QueryBuilder()
                .WithAll<Building>() 
                .WithAny<ResidentialProperty, IndustrialProperty, CommercialProperty>()
                .WithNone<Temp, Deleted, Signature, LinkedParcel, GrowableBuilding>()
                .Build();

            // Update Cycle
            RequireForUpdate(m_SpawnedBuildingQuery);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            m_Log.Debug($"OnUpdate()");
            EntityManager.AddComponent(m_SpawnedBuildingQuery, new ComponentTypeSet(typeof(GrowableBuilding), typeof(LinkedParcel), typeof(Updated)));
        }
    }
}
