// <copyright file="DrawingUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal.Mathematics;
    using Components;
    using Constants;
    using Unity.Mathematics;
    using UnityEngine;
    using static Game.Rendering.OverlayRenderSystem;

    #endregion

    // todo check GizmoBatcher for lots of great drawing utils!
    public static class DrawingUtils {
        public static void DrawLineIcon(Buffer         buffer,
                                        IconDefinition iconDefinition) { }

        public static void DrawParcel(Buffer      buffer,
                                      int2        lotSize,
                                      float4x4    trs,
                                      ParcelState parcelState,
                                      bool        spawnable = false) {
            DrawParcel(buffer, lotSize, trs, ColorConstants.ParcelBackground, parcelState, spawnable);
        }

        public static void DrawParcel(Buffer      buffer,
                                      int2        lotSize,
                                      float4x4    trs,
                                      Color       backgroundColor,
                                      ParcelState parcelState,
                                      bool        spawnable = false) {
            // Final container of lines to draw
            var linesToDraw = new List<LineDef>();

            // Calculate data
            var parcelGeo      = new ParcelGeometry(lotSize);
            var parcelCenter   = parcelGeo.Center;
            var parcelCorners  = parcelGeo.CornerNodes;
            var parcelFront    = parcelGeo.FrontNode;
            var parcelLeft     = parcelGeo.LeftNode;
            var parcelRight    = parcelGeo.RightNode;
            var parcelBack     = parcelGeo.BackNode;
            var hasFrontAccess = (parcelState & ParcelState.RoadFront) == ParcelState.RoadFront;
            var hasLeftAccess  = (parcelState & ParcelState.RoadLeft)  == ParcelState.RoadLeft;
            var hasRightAccess = (parcelState & ParcelState.RoadRight) == ParcelState.RoadRight;
            var accessMult     = 3;

            // Calculate outside edge lines
            var lineWidth     = DimensionConstants.ParcelOutlineWidth;
            var halfLineWidth = DimensionConstants.ParcelOutlineWidth * 0.5f;

            // Four float3 vectors representing the corners in clockwise direction. <br/>
            //  c2 ┌┐ c3. <br/>
            //  c1 └┘ c0. <br/>
            var frontLineWidth = hasFrontAccess ? lineWidth * accessMult : lineWidth;
            var frontLineHalf  = frontLineWidth / 2;
            var leftLineWidth  = hasLeftAccess ? lineWidth * accessMult : lineWidth;
            var leftLineHalf   = leftLineWidth / 2;
            var backLineWidth  = lineWidth;
            var backLineHalf   = backLineWidth / 2;
            var rightLineWidth = hasRightAccess ? lineWidth * accessMult : lineWidth;
            var rightLineHalf  = rightLineWidth / 2;
            // Front
            linesToDraw.Add(
                new LineDef(
                    parcelCorners.c0 + new float3(+rightLineWidth, 0f, -frontLineHalf),
                    parcelCorners.c1 + new float3(-leftLineWidth, 0f, -frontLineHalf),
                    ColorConstants.ParcelOutline,
                    frontLineWidth));
            // Left
            linesToDraw.Add(
                new LineDef(
                    parcelCorners.c1 + new float3(-leftLineHalf, 0f, 0f),
                    parcelCorners.c2 + new float3(-leftLineHalf, 0f, 0f),
                    ColorConstants.ParcelOutline,
                    leftLineWidth));
            // Back
            linesToDraw.Add(
                new LineDef(
                    parcelCorners.c2 + new float3(-leftLineWidth, 0f, +backLineHalf),
                    parcelCorners.c3 + new float3(+rightLineWidth, 0f, +backLineHalf),
                    ColorConstants.ParcelOutline,
                    backLineWidth));
            // Right
            linesToDraw.Add(
                new LineDef(
                    parcelCorners.c3 + new float3(+rightLineHalf, 0f, 0f),
                    parcelCorners.c0 + new float3(+rightLineHalf, 0f, 0f),
                    ColorConstants.ParcelOutline,
                    rightLineWidth));

            // Add background
            backgroundColor.a = ColorConstants.OpacityLow;
            linesToDraw.Add(new LineDef(parcelFront, parcelBack, backgroundColor, parcelGeo.Size.x));

            // Calculate inner lines (front to back)
            var frontNode = new float3(parcelCorners.c1);
            var backNode  = new float3(parcelCorners.c2);
            frontNode.z += -frontLineWidth;
            backNode.z += +backLineWidth;
            for (var i = 1; i < lotSize.x; i++) {
                frontNode.x -= DimensionConstants.CellSize;
                backNode.x  -= DimensionConstants.CellSize;
                linesToDraw.Add(new LineDef(
                                    frontNode, 
                                    backNode, 
                                    ColorConstants.ParcelInline, 
                                    DimensionConstants.ParcelCellOutlineWidth)
                    );
            }

            // Calculate perpendicular inner lines (left to right)
            var leftNode  = new float3(parcelCorners.c1);
            var rightNode = new float3(parcelCorners.c0);
            leftNode.x += -leftLineWidth;
            rightNode.x += +rightLineWidth;
            for (var i = 1; i < lotSize.y; i++) {
                leftNode.z  -= DimensionConstants.CellSize;
                rightNode.z -= DimensionConstants.CellSize;
                linesToDraw.Add(new LineDef(
                                    leftNode, 
                                    rightNode, 
                                    ColorConstants.ParcelInline, 
                                    DimensionConstants.ParcelCellOutlineWidth)
                    );
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

            // Draw FrontAccess Indicator
            buffer.DrawCircle(
                ColorConstants.ParcelFrontIndicator,
                spawnable ? ColorConstants.ParcelFrontIndicator : new Color(1f, 1f, 1f, 0),
                DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
                StyleFlags.Grid,
                new float2(1, 1),
                ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelFront),
                DimensionConstants.ParcelFrontIndicatorDiameter
            );

            //buffer.DrawCircle(
            //    ColorConstants.ParcelFrontIndicator,
            //    Color.red,
            //    DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
            //    StyleFlags.Grid,
            //    new float2(1, 1),
            //    ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c0),
            //    DimensionConstants.ParcelFrontIndicatorDiameter
            //);
            //buffer.DrawCircle(
            //    ColorConstants.ParcelFrontIndicator,
            //    Color.blue,
            //    DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
            //    StyleFlags.Grid,
            //    new float2(1, 1),
            //    ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c1),
            //    DimensionConstants.ParcelFrontIndicatorDiameter
            //);
            //buffer.DrawCircle(
            //    ColorConstants.ParcelFrontIndicator,
            //    Color.green,
            //    DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
            //    StyleFlags.Grid,
            //    new float2(1, 1),
            //    ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c2),
            //    DimensionConstants.ParcelFrontIndicatorDiameter
            //);
            //buffer.DrawCircle(
            //    ColorConstants.ParcelFrontIndicator,
            //    Color.yellow,
            //    DimensionConstants.ParcelFrontIndicatorHollowLineWidth,
            //    StyleFlags.Grid,
            //    new float2(1, 1),
            //    ParcelUtils.GetWorldPosition(trs, parcelCenter, parcelCorners.c3),
            //    DimensionConstants.ParcelFrontIndicatorDiameter
            //);
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