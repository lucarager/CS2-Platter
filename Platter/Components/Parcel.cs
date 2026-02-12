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

    /// <summary>
    /// Flags used to track the state of a parcel (zoning type, road connections).
    /// </summary>
    [Flags]
    public enum ParcelState : byte {
        None          = 0,
        ZoningUniform = 1,
        ZoningMixed   = 2,
        RoadLeft      = 4,
        RoadRight     = 8,
        RoadBack      = 16,
        RoadFront     = 32,
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
        /// Position of this parcel on the road edge (as a curve parameter from 0 to 1).
        /// </summary>
        public float m_CurvePosition;

        /// <summary>
        /// Zoning type applied to this parcel.
        /// </summary>
        public ZoneType m_PreZoneType;

        /// <summary>
        /// State flags indicating zoning type and road connections.
        /// </summary>
        public ParcelState m_State;

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
            m_State = (ParcelState)stateBytes;
        }
    }
}