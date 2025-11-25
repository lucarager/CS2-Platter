// <copyright file="Parcel.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    #region Using Statements

    using System;
    using Colossal.Serialization.Entities;
    using Game.Zones;
    using Unity.Entities;

    #endregion

    [Flags]
    public enum ParcelStateFlags : byte {
        None          = 0,
        ZoningUniform = 1,
        ZoningMixed   = 2,
        RoadLeft      = 4,
        RoadRight     = 8,
        RoadBack      = 16,
    }

    /// <summary>
    /// A Parcel's primary data.
    /// </summary>
    public struct Parcel : IComponentData, ISerializable {
        /// <summary>
        /// Road this parcel is connected to (null if unconnected).
        /// </summary>
        public Entity m_RoadEdge;

        /// <summary>
        /// Building this parcel is connected to (null if unconnected).
        /// </summary>
        public Entity m_Building;

        /// <summary>
        /// Position of this parcel on the road edge.
        /// </summary>
        public float m_CurvePosition;

        /// <summary>
        /// Zoning on this parcel.
        /// </summary>
        public ZoneType m_PreZoneType;

        public ParcelStateFlags m_State;

        /// <inheritdoc/>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_PreZoneType);
            writer.Write(m_RoadEdge);
            writer.Write(m_CurvePosition);
            writer.Write(m_Building);
            writer.Write((byte)m_State);
        }

        /// <inheritdoc/>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_PreZoneType);
            reader.Read(out m_RoadEdge);
            reader.Read(out m_CurvePosition);
            reader.Read(out m_Building);
            reader.Read(out byte stateBytes);
            m_State = (ParcelStateFlags)stateBytes;
        }
    }
}