// <copyright file="PrefabVersion.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Component holding the version of a prefab. Used for migrations and compatibility checking.
    /// </summary>
    public struct PrefabVersion : IComponentData, ISerializable {
        /// <summary>
        /// The version number of the prefab.
        /// </summary>
        public int m_Version;

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_Version);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_Version);
        }
    }
}
