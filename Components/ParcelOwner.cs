// <copyright file="ParcelOwner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Component that marks an entity as owned by a parcel.
    /// Used to link zone blocks or buildings to their parent parcel.
    /// </summary>
    public struct ParcelOwner : IComponentData, ISerializable {
        /// <summary>
        /// Entity reference to the owning parcel.
        /// </summary>
        public Entity m_Owner;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelOwner"/> struct.
        /// </summary>
        /// <param name="owner">The entity reference of the owning parcel.</param>
        public ParcelOwner(Entity owner) {
            m_Owner = owner;
        }

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_Owner);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_Owner);
        }
    }
}
