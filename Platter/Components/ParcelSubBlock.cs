// <copyright file="ParcelSubBlock.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Buffer element storing a reference to a sub-block entity that belongs to a parcel.
    /// A parcel typically contains multiple zone sub-blocks.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ParcelSubBlock : IBufferElementData, IEquatable<ParcelSubBlock>, IEmptySerializable {
        /// <summary>
        /// Entity reference to the sub-block.
        /// </summary>
        public Entity m_SubBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelSubBlock"/> struct.
        /// </summary>
        /// <param name="block">The entity reference of the sub-block.</param>
        public ParcelSubBlock(Entity block) {
            m_SubBlock = block;
        }

        /// <inheritdoc/>
        public bool Equals(ParcelSubBlock other) {
            return m_SubBlock.Equals(other.m_SubBlock);
        }

        /// <inheritdoc/>
        public override int GetHashCode() {
            return m_SubBlock.GetHashCode();
        }
    }
}