// <copyright file="OverlayDrawCommand.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Mathematics;
    using UnityEngine;

    #endregion

    /// <summary>
    /// Discriminator for overlay draw command types.
    /// </summary>
    internal enum OverlayCommandType : byte {
        Line,
        Circle,
    }

    /// <summary>
    /// A Burst-compatible draw command emitted by the parallel prepare job
    /// and consumed by the sequential render job.
    /// </summary>
    internal struct OverlayDrawCommand {
        public OverlayCommandType m_Type;
        public Color              m_OutlineColor;
        public Color              m_FillColor;
        public float3             m_PointA;       // Line: segment start | Circle: position
        public float3             m_PointB;       // Line: segment end   | Circle: unused
        public float              m_Width;        // Line: line width    | Circle: diameter
        public float              m_OutlineWidth; // Both
        public float2             m_Extra;        // Line: roundness     | Circle: direction
        public int                m_StyleFlags;   // Both
    }
}
