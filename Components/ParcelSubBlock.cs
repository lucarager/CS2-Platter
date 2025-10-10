namespace Platter.Components {
    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;

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