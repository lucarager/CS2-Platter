// <copyright file="BuildingBoundaries.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Unity.Entities;
    using Unity.Mathematics;

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