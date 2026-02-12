// <copyright file="ParcelSpawnable.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Marker component indicating that a parcel allows buildings to spawn on it naturally or through zone placement.
    /// </summary>
    public struct ParcelSpawnable : IComponentData, IEmptySerializable {
    }
}
