// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Platter.Components {
    /// <summary>
    /// Marker component indicating that a parcel's boundaries have been shifted.
    /// </summary>
    public struct BoundaryShifted : IComponentData, IEmptySerializable {
    }
}
