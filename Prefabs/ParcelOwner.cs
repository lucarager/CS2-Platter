using System;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Platter.Prefabs {
    public struct ParcelOwner : IComponentData, IQueryTypeParameter, ISerializable {
        public ParcelOwner(Entity owner) {
            this.m_Owner = owner;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(this.m_Owner);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out this.m_Owner);
        }

        public Entity m_Owner;
    }
}
