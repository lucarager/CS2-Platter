// <copyright file="ParcelData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct ParcelData : IComponentData, ISerializable {
        public int2 m_LotSize;
        public Entity m_ZoneBlockPrefab;

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_LotSize);
            writer.Write(m_ZoneBlockPrefab);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_LotSize);
            reader.Read(out m_ZoneBlockPrefab);
        }
    }
}
