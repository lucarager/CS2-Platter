// <copyright file="ParcelSpawnable.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Marker component indicating that an entity is a temporary placeholder for a parcel that is being placed or manipulated.
    /// </summary>
    public struct ParcelPlaceholder : IComponentData, IEmptySerializable {
    }
}
