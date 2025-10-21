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
        public int      index;
        public bool4    projectionFilter;
        public float2x4 projectionConfig;
    }

    public struct BoundarySubAreaNodeData : IBufferElementData {
        public int      absIndex;
        public int      relIndex;
        public int      areaIndex;
        public bool4    projectionFilter;
        public float2x4 projectionConfig;
    }

    public struct BoundarySubLaneData : IBufferElementData {
        public int      index;
        public float2x4 projectionConfig0;
        public float2x4 projectionConfig1;
        public float2x4 projectionConfig2;
        public float2x4 projectionConfig3;
    }

    public struct BoundarySubNetData : IBufferElementData {
        public int      index;
        public float2x4 projectionConfig0;
        public float2x4 projectionConfig1;
        public float2x4 projectionConfig2;
        public float2x4 projectionConfig3;
    }
}
