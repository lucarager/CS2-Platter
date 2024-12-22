// <copyright file="Parcel.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Game.Zones;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public struct Parcel : IComponentData, IQueryTypeParameter, ISerializable {
        /// <summary>
        /// todo.
        /// </summary>
        public Entity m_RoadEdge;

        /// <summary>
        /// todo.
        /// </summary>
        public float m_CurvePosition;

        /// <summary>
        /// todo.
        /// </summary>
        public ZoneType m_PreZoneType;

        /// <summary>
        /// todo.
        /// </summary>
        public bool m_AllowSpawning;

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_PreZoneType);
            writer.Write(m_AllowSpawning);
            writer.Write(m_RoadEdge);
            writer.Write(m_CurvePosition);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_PreZoneType);
            reader.Read(out m_AllowSpawning);
            reader.Read(out m_RoadEdge);
            reader.Read(out m_CurvePosition);
        }
    }
}
