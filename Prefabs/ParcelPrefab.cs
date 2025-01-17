// <copyright file="ParcelPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    using System.Collections.Generic;
    using Game.Zones;
    using Platter.Components;
    using Unity.Entities;

    /// <summary>
    /// Todo.
    /// </summary>
    public class ParcelPrefab : StaticObjectPrefab {
        /// <summary>
        /// Todo.
        /// </summary>
        public int m_LotWidth = 2;

        /// <summary>
        /// Todo.
        /// </summary>
        public int m_LotDepth = 2;

        /// <summary>
        /// Todo.
        /// </summary>
        public ZoneBlockPrefab m_ZoneBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelPrefab"/> class.
        /// </summary>
        public ParcelPrefab() {
        }

        /// <summary>
        /// Gets the parcel's LotSize.
        /// </summary>
        public int LotSize => this.m_LotWidth * this.m_LotDepth;

        /// <inheritdoc/>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (this.m_ZoneBlock != null) {
                prefabs.Add(this.m_ZoneBlock);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);

            components.Add(ComponentType.ReadWrite<PlaceableObjectData>());
            components.Add(ComponentType.ReadWrite<ParcelData>());

            // components.Add(ComponentType.ReadWrite<BuildingData>());
            // components.Add(ComponentType.ReadWrite<BuildingTerraformData>());
        }

        /// <inheritdoc/>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);

            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<SubBlock>());
            components.Add(ComponentType.ReadWrite<SubLane>());
            components.Add(ComponentType.ReadWrite<SubObject>());
        }
    }
}
