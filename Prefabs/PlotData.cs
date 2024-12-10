// <copyright file="PlotData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Prefabs {
    using Game.Objects;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct PlotData : IComponentData, IQueryTypeParameter {
        public float2 m_ForwardDirection;
        public int2 m_PlotSize;
        public Transform m_PlotTransform;
    }
}
