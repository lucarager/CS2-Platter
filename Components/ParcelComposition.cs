// <copyright file="ParcelComposition.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public struct ParcelComposition : IComponentData, IQueryTypeParameter {
        /// <summary>
        /// todo.
        /// </summary>
        public Entity m_ZoneBlockPrefab;
    }
}
