// <copyright file="ParcelData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Stores parcel-specific data including lot size and associated zone block prefab.
    /// </summary>
    public struct ParcelData : IComponentData, ISerializable {
        /// <summary>
        /// The lot size dimensions as (width, depth) in cells.
        /// </summary>
        public int2 m_LotSize;

        /// <summary>
        /// Entity reference to the zone block prefab used for this parcel.
        /// </summary>
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
