// <copyright file="ParcelSubBlock.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    [InternalBufferCapacity(1)]
    public struct ParcelSubBlock : IBufferElementData, IEquatable<ParcelSubBlock>, IEmptySerializable {
        public Entity m_SubBlock;

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