// <copyright file="ParcelOwner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    public struct LinkedParcel : IComponentData, ISerializable {
        public Entity m_Parcel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelOwner"/> struct.
        /// </summary>
        /// <param name="owner"></param>
        public LinkedParcel(Entity owner) {
            m_Parcel = owner;
        }

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_Parcel);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_Parcel);
        }
    }
}
