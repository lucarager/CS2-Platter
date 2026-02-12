// <copyright file="ParcelPlaceholderPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    #region Using Statements

    using System.Collections.Generic;
    using Platter.Components;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Prefab base of a parcel.
    /// </summary>
    public class ParcelPlaceholderPrefab : StaticObjectPrefab {
        public int             m_LotWidth = 2;
        public int             m_LotDepth = 2;
        public ZoneBlockPrefab m_ZoneBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelPlaceholderPrefab"/> class.
        /// </summary>
        public ParcelPlaceholderPrefab() { }

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
            components.Add(ComponentType.ReadWrite<ParcelPlaceholderData>());

            // Experimental
            // components.Add(ComponentType.ReadWrite<ObjectSubAreas>());
        }

        /// <inheritdoc/>
        public override void GetArchetypeComponents(HashSet<ComponentType> components) {
            base.GetArchetypeComponents(components);

            components.Add(ComponentType.ReadWrite<Parcel>());
            components.Add(ComponentType.ReadWrite<ParcelSubBlock>());
            components.Add(ComponentType.ReadWrite<ParcelPlaceholder>());
        }
    }
}