using System;
using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Platter.Components {

    public struct ConnectedParcel : IBufferElementData, IEquatable<ConnectedParcel>, IEmptySerializable {
        public Entity m_Parcel;

        public ConnectedParcel(Entity parcel) {
            m_Parcel = parcel;
        }

        public bool Equals(ConnectedParcel other) {
            return m_Parcel.Equals(other.m_Parcel);
        }

        public override int GetHashCode() {
            return m_Parcel.GetHashCode();
        }
    }
}
