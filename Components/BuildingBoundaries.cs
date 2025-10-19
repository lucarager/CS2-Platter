// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Mathematics;
using Unity.Mathematics;

namespace Platter.Components {
    using Colossal.Serialization.Entities;
    using Game.Prefabs;
    using Unity.Entities;

    public struct BoundarySubObjectData : IBufferElementData {
        public float3       m_Position;
        public quaternion   m_Rotation;
        public Hash128 m_Id;
        public int m_ParentMesh;
        public int m_GroupIndex;
    }

    public struct BoundarySubAreaData : IBufferElementData {
        public float3 m_NodePosition;
        public int    m_ParentMesh;
    }

    public struct BoundarySubLaneData : IBufferElementData {
        public Bezier4x3 m_BezierCurve;
        public int2      m_NodeIndex;
        public int2      m_ParentMesh;
    }
}
