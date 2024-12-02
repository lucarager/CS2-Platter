namespace Platter.Prefabs {
    using System;
    using Unity.Entities;
    using Unity.Mathematics;
    using Colossal.Serialization.Entities;

    public struct ParcelData : IComponentData, IQueryTypeParameter, ISerializable {
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(this.m_LotSize);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out this.m_LotSize);
        }

        public int2 m_LotSize;
        public Entity m_ZoneBlockPrefab;
    }
}
