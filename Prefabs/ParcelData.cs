// <copyright file="ParcelData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Prefabs {
    using Colossal.Serialization.Entities;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct ParcelData : IComponentData, IQueryTypeParameter, ISerializable {
        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(this.m_LotSize);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out this.m_LotSize);
        }

        public int2 m_LotSize;
        public Entity m_ZoneBlockPrefab;
    }
}
