// <copyright file="ParcelOwner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    public struct ParcelOwner : IComponentData, IQueryTypeParameter, ISerializable {
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

        public Entity m_Owner;
    }
}
