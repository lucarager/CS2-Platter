// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Game.Objects;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// A component used to record where an object was placed so that it doesn't drop by accident.
    /// Originally written by @yenyang for Anarchy
    /// </summary>
    public struct CachedTransform : IComponentData, IQueryTypeParameter, ISerializable {
        /// <summary>
        /// The position record from original transform.
        /// </summary>
        public float3 m_Position;

        /// <summary>
        /// The rotation record from orginal transform.
        /// </summary>
        public quaternion m_Rotation;

        /// <summary>
        /// Gets a game.objects.Transform from the transform record.
        /// </summary>
        public Game.Objects.Transform Transform => new(m_Position, m_Rotation);

        /// <summary>
        /// Evaluates equualitiy between a transform record and a transform.
        /// </summary>
        /// <param name="other">A transform struct.</param>
        /// <returns>True if transform record matches the transform.</returns>
        public bool Equals(Transform other) {
            return m_Position.Equals(other.m_Position) && m_Rotation.Equals(other.m_Rotation);
        }

        /// <summary>
        /// Serializes the transform record.
        /// </summary>
        /// <typeparam name="TWriter">Part of serialization.</typeparam>
        /// <param name="writer">Part of serialization writing.</param>
        public void Serialize<TWriter>(TWriter writer)
            where TWriter : IWriter {
            writer.Write(m_Position);
            writer.Write(m_Rotation);
        }

        /// <summary>
        /// Deserializes the transform record.
        /// </summary>
        /// <typeparam name="TReader">Part of deserialization.</typeparam>
        /// <param name="reader">Reader for deserialization.</param>
        public void Deserialize<TReader>(TReader reader)
            where TReader : IReader {
            reader.Read(out m_Position);
            reader.Read(out m_Rotation);
            if (math.all(m_Position >= -100000f) && math.all(m_Position <= 100000f) &&
                math.all(math.isfinite(m_Rotation.value))) {
                return;
            }

            m_Position = default(float3);
            m_Rotation = quaternion.identity;
        }
    }
}