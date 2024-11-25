using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Mathematics;

namespace Platter.Prefabs {
    internal class ParcelData : IComponentData, IQueryTypeParameter {
        public float3 m_Position;
        public float2 m_Direction;
        public int2 m_Size;
        public Entity m_ZoneBlock;
    }
}
