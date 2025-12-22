// <copyright file="BuildingBoundaries.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Components {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Stores boundary data for sub-objects including index and projection configuration.
    /// </summary>
    public struct BoundarySubObjectData : IBufferElementData {
        /// <summary>
        /// Index of the sub-object.
        /// </summary>
        public int      index;

        /// <summary>
        /// Boolean filter for projection (4 flags for different projection directions).
        /// </summary>
        public bool4    projectionFilter;

        /// <summary>
        /// Projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig;
    }

    /// <summary>
    /// Stores boundary data for sub-area nodes including indices and projection configuration.
    /// </summary>
    public struct BoundarySubAreaNodeData : IBufferElementData {
        /// <summary>
        /// Absolute index of the area node.
        /// </summary>
        public int      absIndex;

        /// <summary>
        /// Relative index within the area.
        /// </summary>
        public int      relIndex;

        /// <summary>
        /// Index of the area this node belongs to.
        /// </summary>
        public int      areaIndex;

        /// <summary>
        /// Boolean filter for projection (4 flags for different projection directions).
        /// </summary>
        public bool4    projectionFilter;

        /// <summary>
        /// Projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig;
    }

    /// <summary>
    /// Stores boundary data for sub-lanes including index and projection configurations.
    /// </summary>
    public struct BoundarySubLaneData : IBufferElementData {
        /// <summary>
        /// Index of the lane.
        /// </summary>
        public int      index;

        /// <summary>
        /// First projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig0;

        /// <summary>
        /// Second projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig1;

        /// <summary>
        /// Third projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig2;

        /// <summary>
        /// Fourth projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig3;
    }

    /// <summary>
    /// Stores boundary data for sub-networks including index and projection configurations.
    /// </summary>
    public struct BoundarySubNetData : IBufferElementData {
        /// <summary>
        /// Index of the network.
        /// </summary>
        public int      index;

        /// <summary>
        /// First projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig0;

        /// <summary>
        /// Second projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig1;

        /// <summary>
        /// Third projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig2;

        /// <summary>
        /// Fourth projection configuration matrix (2x4) for rendering boundaries.
        /// </summary>
        public float2x4 projectionConfig3;
    }
}