// <copyright file="DrawingUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal.Mathematics;
    using Constants;
    using Unity.Mathematics;
    using UnityEngine;
    using static Game.Rendering.OverlayRenderSystem;

    #endregion

    public static class DrawingUtils {
        public static void DrawLineIcon(Buffer         buffer,
                                        IconDefinition iconDefinition) { }

        public static void DrawParcel(Buffer   buffer,
                                      int2     lotSize,
                                      float4x4 trs,
                                      bool     spawnable = false) {
            DrawParcel(buffer, lotSize, trs, ColorConstants.ParcelBackground, spawnable);
        }

        public static void DrawParcel(Buffer   buffer,
                                      int2     lotSize,
                                      float4x4 trs,
                                      Color    backgroundColor,
                                      bool     spawnable = false) {
            // Final container of lines to draw
            var linesToDraw = new List<LineDef>();

            // Calculate data
            var parcelGeo     = new ParcelGeometry(lotSize);
            var parcelCenter  = parcelGeo.Center;
            var parcelCorners = parcelGeo.CornerNodes;
            var parcelFront   = parcelGeo.FrontNode;
            var parcelBack    = parcelGeo.BackNode;

            // Calculate outside edge lines
            linesToDraw.Add(new LineDef(parcelCorners.c0, parcelCorners.c1, ColorConstants.ParcelOutline, DimensionConstants.ParcelOutlineWidth));
            linesToDraw.Add(new LineDef(parcelCorners.c1, parcelCorners.c2, ColorConstants.ParcelOutline, DimensionConstants.ParcelOutlineWidth));
            linesToDraw.Add(new LineDef(parcelCorners.c2, parcelCorners.c3, ColorConstants.ParcelOutline, DimensionConstants.ParcelOutlineWidth));
            linesToDraw.Add(new LineDef(parcelCorners.c3, parcelCorners.c0, ColorConstants.ParcelOutline, DimensionConstants.ParcelOutlineWidth));

            // Ensure background opacity
            backgroundColor.a = ColorConstants.OpacityLow;

            // Add background
            linesToDraw.Add(new LineDef(parcelFront, parcelBack, backgroundColor, parcelGeo.Size.x));

            // Calculate inner lines
            var frontNode = new float3(parcelCorners.c1);
            var backNode  = new float3(parcelCorners.c2);
            for (var i = 1; i < lotSize.x; i++) {
                frontNode.x += DimensionConstants.CellSize;
                backNode.x  += DimensionConstants.CellSize;
                linesToDraw.Add(new LineDef(frontNode, backNode, ColorConstants.ParcelInline, DimensionConstants.ParcelCellOutlineWidth));
            }

            // Calculate perpendicular inner lines
            var rightNode = new float3(parcelCorners.c0);
            var leftNode  = new float3(parcelCorners.c1);
            for (var i = 1; i < lotSize.y; i++) {
                leftNode.z  += DimensionConstants.CellSize;
                rightNode.z += DimensionConstants.CellSize;
                linesToDraw.Add(new LineDef(leftNode, rightNode, ColorConstants.ParcelInline, DimensionConstants.ParcelCellOutlineWidth));
            }

            // Draw all lines
            foreach (var lineDef in linesToDraw) {
                buffer.DrawLine(
                    lineDef.color,
                    new Line3.Segment(
                        ParcelUtils.GetWorldPosition(trs, parcelCenter, lineDef.startNode),
                        ParcelUtils.GetWorldPosition(trs, parcelCenter, lineDef.endNode)
                    ),
                    lineDef.width
                );
            }

            // Draw "Front Circle"
            buffer.DrawCircle(
                ColorConstants.ParcelFrontIndicator,
                spawnable ? ColorConstants.ParcelFrontIndicator : new Color(1f, 1f, 1f, 0),
                DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
                StyleFlags.Grid,
                new float2(1, 1),
                ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelFront),
                DimensionConstants.ParcelFrontIndicatorDiameter
            );
        }

        public struct LineDef {
            public float3 startNode;
            public float3 endNode;
            public Color  color;
            public float  width;

            /// <summary>
            /// Initializes a new instance of the <see cref="LineDef"/> struct.
            /// </summary>
            /// <param name="startNode"></param>
            /// <param name="endNode"></param>
            /// <param name="color"></param>
            /// <param name="width"></param>
            public LineDef(float3 startNode,
                           float3 endNode,
                           Color  color,
                           float  width) {
                this.startNode = startNode;
                this.endNode   = endNode;
                this.color     = color;
                this.width     = width;
            }
        }

        public struct PathDef {
            public string[] path;
            public Color    color;
        }

        public struct IconDefinition {
            public PathDef[] paths;
        }
    }
}