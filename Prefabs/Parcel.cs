namespace Platter.Prefabs {
    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;

    public struct Parcel : IComponentData, IQueryTypeParameter, ISerializable {
        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(this.m_RoadEdge);
            writer.Write(this.m_CurvePosition);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out this.m_RoadEdge);
            reader.Read(out this.m_CurvePosition);
        }

        public Entity m_RoadEdge;
        public float m_CurvePosition;
    }
}
