// <copyright file="ParcelOwner.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    /// <summary>
    /// Component that links an entity to a parcel (e.g., a zone block or building linked to its parent parcel).
    /// </summary>
    public struct LinkedParcel : IComponentData, ISerializable {
        /// <summary>
        /// Entity reference to the linked parcel.
        /// </summary>
        public Entity m_Parcel;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkedParcel"/> struct.
        /// </summary>
        /// <param name="parcel">The entity reference of the linked parcel.</param>
        public LinkedParcel(Entity parcel) {
            m_Parcel = parcel;
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
