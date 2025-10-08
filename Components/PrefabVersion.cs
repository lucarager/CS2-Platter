namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Game.Zones;
    using Unity.Entities;

    /// <summary>
    /// todo.
    /// </summary>
    public struct PrefabVersion : IComponentData, IQueryTypeParameter, ISerializable {
        public uint m_Version;

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
