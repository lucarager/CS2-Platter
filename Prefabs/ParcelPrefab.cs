// <copyright file="ParcelPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    using Colossal.Logging;
    using Game.Objects;
    using Game.Zones;
    using Platter;
    using Platter.Components;
    using Platter.Systems;
    using Platter.Utils;
    using System.Collections.Generic;
    using Unity.Entities;
    using Unity.Mathematics;

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
        /// todo.
        /// </summary>
        public ZoneType m_PreZoneType;

        /// <summary>
        /// todo.
        /// </summary>
        public bool m_AllowSpawning;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelPrefab"/> class.
        /// </summary>
        public ParcelPrefab() {
        }

        /// <summary>
        /// Gets the parcel's LotSize.
        /// </summary>
        public int LotSize => m_LotWidth * m_LotDepth;

        /// <inheritdoc/>
        public override void GetDependencies(List<PrefabBase> prefabs) {
            base.GetDependencies(prefabs);
            if (m_ZoneBlock != null) {
                prefabs.Add(m_ZoneBlock);
            }
        }

        /// <inheritdoc/>
        public override void GetPrefabComponents(HashSet<ComponentType> components) {
            base.GetPrefabComponents(components);

            components.Add(ComponentType.ReadWrite<ParcelData>());
            components.Add(ComponentType.ReadWrite<PlaceableObjectData>());

            // Experimental
            // components.Add(ComponentType.ReadWrite<ObjectSubAreas>());
        }

        /// <inheritdoc/>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);

            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelComposition>());
            components.Add(ComponentType.ReadWrite<ParcelSubBlock>());
        }
    }
}
