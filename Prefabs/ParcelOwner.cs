// <copyright file="ParcelOwner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Prefabs {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    public struct ParcelOwner : IComponentData, IQueryTypeParameter, ISerializable {
        public ParcelOwner(Entity owner) {
            this.m_Owner = owner;
        }

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(this.m_Owner);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out this.m_Owner);
        }

        public Entity m_Owner;
    }
}
