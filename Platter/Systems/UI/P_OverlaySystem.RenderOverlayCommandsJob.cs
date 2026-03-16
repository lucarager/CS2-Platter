// <copyright file="P_OverlaySystem.RenderOverlayCommandsJob.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Rendering;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Jobs;

    #endregion

    public partial class P_OverlaySystem {
        /// <summary>
        /// Sequential job that reads pre-computed <see cref="OverlayDrawCommand"/>s from a
        /// <see cref="NativeStream"/> and dispatches them to the <see cref="OverlayRenderSystem.Buffer"/>.
        /// </summary>
#if USE_BURST
        [BurstCompile]
#endif
        internal struct RenderOverlayCommandsJob : IJob {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_OverlayRenderBuffer;
            [ReadOnly] public required NativeStream.Reader        m_CommandReader;
                       public required int                        m_ForEachCount;

            public void Execute() {
                for (var foreachIndex = 0; foreachIndex < m_ForEachCount; foreachIndex++) {
                    var remaining = m_CommandReader.BeginForEachIndex(foreachIndex);

                    while (remaining > 0) {
                        var cmd = m_CommandReader.Read<OverlayDrawCommand>();
                        remaining--;

                        switch (cmd.m_Type) {
                            case OverlayCommandType.Line:
                                m_OverlayRenderBuffer.DrawLine(
                                    cmd.m_OutlineColor,
                                    cmd.m_FillColor,
                                    cmd.m_OutlineWidth,
                                    (OverlayRenderSystem.StyleFlags)cmd.m_StyleFlags,
                                    new Line3.Segment(cmd.m_PointA, cmd.m_PointB),
                                    cmd.m_Width,
                                    cmd.m_Extra);
                                break;
                            case OverlayCommandType.Circle:
                                m_OverlayRenderBuffer.DrawCircle(
                                    cmd.m_OutlineColor,
                                    cmd.m_FillColor,
                                    cmd.m_OutlineWidth,
                                    (OverlayRenderSystem.StyleFlags)cmd.m_StyleFlags,
                                    cmd.m_Extra,
                                    cmd.m_PointA,
                                    cmd.m_Width);
                                break;
                        }
                    }

                    m_CommandReader.EndForEachIndex();
                }
            }
        }
    }
}
