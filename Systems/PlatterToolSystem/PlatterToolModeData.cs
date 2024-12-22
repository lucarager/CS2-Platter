// <copyright file="PlatterToolModeData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using static Game.Rendering.GuideLinesSystem;
    using Color = UnityEngine.Color;
    using Transform = Game.Objects.Transform;

    /// <summary>
    /// PlatterTool placement mode.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Protected fields")]
    public abstract class PlatterToolModeData {
        public Entity SelectedEdgeEntity {
            get; set;
        }

        public Curve SelectedCurve {
            get; set;
        }

        public NetCompositionData SelectedCurveGeo {
            get; set;
        }

        public EdgeGeometry SelectedCurveEdgeGeo {
            get; set;
        }

        public PrefabBase SelectedPrefabBase {
            get; set;
        }

        /// <summary>
        /// Selection radius of points.
        /// </summary>
        protected const float PointRadius = 8f;

        /// <summary>
        /// IndicaintPositionOnCurveether a valid starting position has been recorded.
        /// </summary>
        protected bool m_validStart;

        /// <summary>
        /// RecordsintPositionOnCurveurrent selection start position.
        /// </summary>
        protected float3 m_startPos;

        /// <summary>
        /// RecorintPositionOnCurve current selection end position.
        /// </summary>
        protected float3 m_endPos;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterToolModeData"/> class.
        /// </summary>
        public PlatterToolModeData() {
            // Basic state.
            m_validStart = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlatterToolModeData"/> class.
        /// </summary>
        /// <param name="mode">Mode to copy starting state from.</param>
        public PlatterToolModeData(PlatterToolModeData mode) {
            m_validStart = mode.m_validStart;
            m_startPos = mode.m_startPos;
        }

        /// <summary>
        /// Gets a value indicating whether gets a value indicatintPositionOnCurveether a valid starting position has been recorded.
        /// </summary>
        public bool HasStart => m_validStart;

        /// <summary>
        /// Gets a value indicating whether we're ready to place (we have enough control positions).
        /// </summary>
        public virtual bool HasAllPoints => m_validStart;

        /// <summary>
        /// Handles a mouse click.
        /// </summary>
        /// <paramintPositionOnCurve"position">Click world position.</param>
        /// <returns><c>true</c> if items are to be placed as a result of this click, <c>false</c> otherwise.</returns>
        public virtual bool HandleClick(float3 position) {
            // If no valid start position is set, record it.
            if (!m_validStart) {
                m_startPos = position;
                m_endPos = position;
                m_validStart = true;

                // No placement at this stage (only the first click has been made).
                return false;
            }

            // Second click; we're placing items.
            return true;
        }

        /// <summary>
        /// Performs actions after items are placed on the current line, setting up for the next line to be set.
        /// </summary>
        public virtual void ItemsPlaced() {
            // Update new starting location to the previous end point.
            m_startPos = m_endPos;
        }

        public Vector3 Translate(Bezier4x3 curve, float t, float direction) {
            Vector3 aPos = MathUtils.Position(curve, t);
            Vector3 aTang = MathUtils.Tangent(curve, t);
            Vector3 aPerp = new(aTang.z, aTang.y, -aTang.x);
            return aPos + (aPerp.normalized * 8f * direction);
        }

        public class CurveDef {
            public Bezier4x3 curve;
            public float length;
            public float startLength = 0;
            public float direction = 1;

            /// <summary>
            /// Initializes a new instance of the <see cref="CurveDef"/> class.
            /// </summary>
            /// <param name="curve"></param>
            public CurveDef(Bezier4x3 curve, float direction) {
                this.curve = curve;
                this.length = MathUtils.Length(curve);
                this.direction = direction;
            }
        }

        /// <summary>
        /// Calculates the points to use based on this mode.
        /// </summary>
        /// <param name="cintPositionOnCurvePos">Selection current position.</param>
        /// <param name="spacingMode">Active spacing mode.</param>
        /// <param name="rotationMode">Active rotation mode.</param>
        /// <param name="spacing">Spacing distance.</param>
        /// <param name="randomSpacing">Random spacing offset maximum.</param>
        /// <param name="randomOffset">Random lateral offset maximum.</param>
        /// <param name="rotation">Rotation setting.</param>
        /// <param name="zBounds">Prefab zBounds.</param>
        /// <param name="pointList">List of points to populate.</param>
        /// <param name="heightData">Terrain height data reference.</param>
        public virtual void CalculatePoints(
            float spacing,
            int rotation,
            float offset,
            float size,
            List<Transform> pointList,
            ref TerrainHeightData heightData,
            bool4 sides,
            OverlayRenderSystem.Buffer overlayBuffer
        ) {
            // Make sure there's always a little offset, for directional vector calc
            offset += 0.1f;

            // Don't do anything if we don't have a valid start point.
            if (SelectedEdgeEntity == Entity.Null) {
                return;
            }

            // Curves
            var curvesDict = new Dictionary<string, List<CurveDef>> {
                { "left", new List<CurveDef>() },
                { "right", new List<CurveDef>() },
                { "start", new List<CurveDef>() },
                { "end", new List<CurveDef>() }
            };

            if (sides.x) {
                curvesDict["left"].Add(new CurveDef(SelectedCurveEdgeGeo.m_Start.m_Left, -1f));
                curvesDict["left"].Add(new CurveDef(SelectedCurveEdgeGeo.m_End.m_Left, -1f));
            }

            if (sides.y) {
                curvesDict["right"].Add(new CurveDef(SelectedCurveEdgeGeo.m_Start.m_Right, 1f));
                curvesDict["right"].Add(new CurveDef(SelectedCurveEdgeGeo.m_End.m_Right, 1f));
            }

            foreach (var curves in curvesDict.Values) {
                if (curves.Count == 0) {
                    break;
                }

                var totalLength = 0f;

                for (var i = 0; i < curves.Count; i++) {
                    var curve = curves[i];
                    if (curves.ElementAtOrDefault(i - 1) != null) {
                        curve.startLength = curves[i - 1].startLength + curves[i - 1].length;
                    }

                    totalLength += curve.length;
                }

                var pointsCount = math.floor((totalLength + spacing) / (size + spacing));
                var intervals = pointsCount - 1;
                var stepLength = intervals == 0 ? 0 : totalLength / intervals;

                // Generate points, retrieving the correct position on the correct curve.
                var currentCurveIndex = 0;
                for (int i = 0; i < pointsCount; i++) {
                    var currentPosition = i * stepLength;

                    // Get the right curve given the currentPosition
                    if (currentPosition > curves[currentCurveIndex].startLength + curves[currentCurveIndex].length && currentCurveIndex < curves.Count - 1) {
                        currentCurveIndex++;
                    }

                    var currentCurve = curves[currentCurveIndex];
                    var relativePosition = currentPosition - currentCurve.startLength;
                    var relativePercentage = relativePosition / currentCurve.length;
                    var pointPositionOnCurve = MathUtils.Position(
                        currentCurve.curve,
                        relativePercentage
                    );
                    var tangent = MathUtils.Tangent(currentCurve.curve, relativePercentage);
                    var perpendicular2dVector = new Vector3(tangent.z, 0f, -tangent.x);
                    float3 normal = perpendicular2dVector.normalized;
                    var shiftedPointPosition = pointPositionOnCurve + (normal * offset * currentCurve.direction);
                    Vector3 direction = pointPositionOnCurve - shiftedPointPosition;
                    var perpendicularRotation = Quaternion.LookRotation(direction.normalized);

                    pointList.Add(new Transform {
                        m_Position = shiftedPointPosition,
                        m_Rotation = perpendicularRotation,
                    });
                }
            }

            var colors = new Color[] {
                Color.blue,
                Color.red,
                Color.green,
                Color.yellow,
                Color.white,
                Color.black,
            };

            // Debug Rendering
            foreach (var curves in curvesDict.Values) {
                var cI = 0;
                foreach (var curve in curves) {
                    overlayBuffer.DrawCurve(colors[cI], curve.curve, 1f);
                    cI = (cI < colors.Length - 1) ? cI + 1 : 0;
                }
            }

            var cI2 = 0;
            foreach (Transform point in pointList) {
                overlayBuffer.DrawCircle(colors[cI2], point.m_Position, 3f);
                cI2 = (cI2 < colors.Length - 1) ? cI2 + 1 : 0;
            }
        }

        public void Reset() {
            m_validStart = false;
        }

        ///// <summary>
        ///// Checks to see pointPositionOnCurveck should initiate point dragging.
        ///// </summary>
        ///// <param name="position">Click position in world space.</param>
        ///// <returns>Drag mode.</returns>
        // internal virtual DragMode CheckDragHit(float3 position) {
        //    if (math.distancesq(position, m_startPos) < (PointRadius * PointRadius)) {
        //        // Start point.
        //        return DragMode.StartPos;
        //    } else if (math.distancesq(position, m_endPos) < (PointRadius * PointRadius)) {
        //        // End point.
        //        return DragMode.EndPos;
        //    }

        // // No hit.
        //    return DragMode.None;
        // }

        ///// <summary>
        ///// Handles dragging action.
        ///// </summary>
        ///// <param name="dragMode">Dragging mode.</param>
        ///// <param name="position">New position.</param>
        // internal virtual void HandleDrag(DragMode dragMode, float3 position) {
        //    // Drag start point.
        //    if (dragMode == DragMode.StartPos) {
        //        m_startPos = position;
        //    }
        // }

        /// <summary>
        /// Draws a straight dashed line overlay between the two given points.
        /// </summary>
        /// <param name="startPos">PlatterTool start position.</param>
        /// <param name="endPos">PlatterTool end position.</param>
        /// <param name="segment">PlatterTool segment.</param>
        /// <param name="overlayBuffer">Overlay buffer.</param>
        /// <param name="tooltips">Tooltip list.</param>
        /// <param name="cameraController">Active camera controller instance.</param>
        protected void DrawDashedLine(float3 startPos, float3 endPos, Line3.Segment segment, OverlayRenderSystem.Buffer overlayBuffer, List<TooltipInfo> tooltips, CameraUpdateSystem cameraController) {
            // Semi-transparent white color
            Color color = new(1f, 1f, 1f, 0.6f);

            // Dynamically scale dashed line based on current gameplay camera zoom level; vanilla range min:10f max:10000f.
            float currentZoom = cameraController.zoom;
            float lineScaleModifier = (currentZoom * 0.0025f) + 0.1f;

            float distance = math.distance(startPos.xz, endPos.xz);

            // Don't draw lines for short distances.
            if (distance > lineScaleModifier * 8f) {
                // Offset segment, mimicking game simple curve overlay, to ensure dash spacing.
                float3 offset = (segment.b - segment.a) * (lineScaleModifier * 5f / distance);
                Line3.Segment line = new(segment.a + offset, segment.b - offset);

                // Measurements for dashed line: length of dash, width of dash, and gap between them.
                float lineDashLength = lineScaleModifier * 5f;
                float lineDashWidth = lineScaleModifier * 3f;
                float lineGapLength = lineScaleModifier * 3f;

                // Draw line - distance figures mimic game simple curve overlay.
                overlayBuffer.DrawDashedLine(color, line, lineDashWidth, lineDashLength, lineGapLength);

                // Add length tooltip.
                int length = Mathf.RoundToInt(math.distance(startPos.xz, endPos.xz));
                if (length > 0) {
                    tooltips.Add(new TooltipInfo(TooltipType.Length, (startPos + endPos) * 0.5f, length));
                }
            }
        }

        /// <summary>
        /// Draws a curved dashed line overlay along the given Bezier curve.
        /// </summary>
        /// <param name="curve">PlatterTool curve segment.</param>
        /// <param name="overlayBuffer">Overlay buffer.</param>
        /// <param name="cameraController">Active camera controller instance.</param>
        protected void DrawCurvedDashedLine(Bezier4x3 curve, OverlayRenderSystem.Buffer overlayBuffer, CameraUpdateSystem cameraController) {
            // Semi-transparent white color.
            Color color = new(1f, 1f, 1f, 0.6f);

            // Dynamically scale dashed line based on current gameplay camera zoom level; vanilla range min:10f max:10000f.
            float currentZoom = cameraController.zoom;
            float lineScaleModifier = (currentZoom * 0.0025f) + 0.1f;

            // Measurements for dashed line: length of dash, width of dash, and gap between them.
            float lineDashLength = lineScaleModifier * 4f;
            float lineDashWidth = lineScaleModifier * 1f;
            float lineGapLength = lineScaleModifier * 3f;

            // Draw line - distance figures mimic game simple curve overlay.
            overlayBuffer.DrawDashedCurve(color, curve, lineDashWidth, lineDashLength, lineGapLength);
        }

        /// <summary>
        /// Calculates the 2D XZ angle (in radians) between two points, adding the provided adjustment.
        /// </summary>
        /// <param name="point1">First point.</param>
        /// <param name="point2">Second point.</param>
        /// <param name="adjustment">Adjustment to apply (in radians).</param>
        /// <returns>2D XZ angle from the first to the second point, in radians.</returns>
        protected float CalculateRelativeAngle(float3 point1, float3 point2, float adjustment) {
            // Calculate angle from point 1 to point 2.
            float3 difference = point2 - point1;
            float relativeAngle = math.atan2(difference.x, difference.z) + adjustment;

            // Error check.
            if (float.IsNaN(relativeAngle)) {
                relativeAngle = 0f;
            }

            // Minimum bounds check.
            while (relativeAngle < -math.PI) {
                relativeAngle += math.PI * 2f;
            }

            // Maximum bounds check.
            while (relativeAngle >= math.PI) {
                relativeAngle -= math.PI * 2f;
            }

            return relativeAngle;
        }
    }
}
