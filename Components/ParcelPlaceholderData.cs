// <copyright file="ParcelSpawnable.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Marker component storing data for a parcel placeholder entity during placement or manipulation.
    /// </summary>
    public struct ParcelPlaceholderData : IComponentData, IEmptySerializable {
    }
}
