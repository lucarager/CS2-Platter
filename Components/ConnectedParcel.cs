// <copyright file="ConnectedParcel.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using System;
    using Colossal.Serialization.Entities;
    using Game.Prefabs;
    using Unity.Entities;

    public struct ConnectedParcel : IBufferElementData, IEquatable<ConnectedParcel>, IEmptySerializable, ISerializable {
        public Entity m_Parcel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectedParcel"/> struct.
        /// </summary>
        /// <param name="parcel"></param>
        public ConnectedParcel(Entity parcel) {
            m_Parcel = parcel;
        }

        public bool Equals(ConnectedParcel other) {
            return m_Parcel.Equals(other.m_Parcel);
        }

        public override int GetHashCode() {
            return m_Parcel.GetHashCode();
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
