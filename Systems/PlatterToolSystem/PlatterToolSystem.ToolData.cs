// <copyright file="PlatterToolSystem.ToolData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Platter.Utils;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Transform = Game.Objects.Transform;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        public static Color[] DebugColors = new Color[] {
            Color.blue,
            Color.red,
            Color.green,
            Color.yellow,
            Color.white,
            Color.black,
            Color.cyan,
            Color.magenta
        };

        public abstract class ToolData {
            protected EntityManager m_EntityManager;
            protected PrefabSystem m_PrefabSystem;

            public ToolData(EntityManager entityManager, PrefabSystem prefabSystem) {
                m_EntityManager = entityManager;
                m_PrefabSystem = prefabSystem;
            }
        }

        public class ToolData_Plop : ToolData {
            public ToolData_Plop(EntityManager entityManager, PrefabSystem prefabSystem)
                : base(entityManager, prefabSystem) {
            }
        }

        public class ToolData_RoadEditor : ToolData {
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

            public StartNodeGeometry SelectedCurveStartGeo {
                get; set;
            }

            public EndNodeGeometry SelectedCurveEndGeo {
                get; set;
            }

            public PrefabBase SelectedPrefabBase {
                get; set;
            }

            public ToolData_RoadEditor(EntityManager entityManager, PrefabSystem prefabSystem)
                : base(entityManager, prefabSystem) {
            }

            /// <inheritdoc/>
            public bool Start(Entity entity) {
                if (!(
                        m_EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef) &&
                        m_EntityManager.TryGetComponent<Curve>(entity, out var curve) &&
                        m_EntityManager.TryGetComponent<EdgeGeometry>(entity, out var edgeGeo) &&
                        m_EntityManager.TryGetComponent<StartNodeGeometry>(entity, out var startGeo) &&
                        m_EntityManager.TryGetComponent<EndNodeGeometry>(entity, out var endGeo) &&
                        m_EntityManager.TryGetComponent<Composition>(entity, out var composition) &&
                        m_EntityManager.TryGetComponent<NetCompositionData>(
                            composition.m_Edge,
                            out var edgeNetCompData) &&
                        m_PrefabSystem.TryGetPrefab<PrefabBase>(prefabRef, out var prefabBase))
                    ) {
                    return false;
                }

                SelectedEdgeEntity = entity;
                SelectedCurve = curve;
                SelectedCurveGeo = edgeNetCompData;
                SelectedCurveEdgeGeo = edgeGeo;
                SelectedCurveStartGeo = startGeo;
                SelectedCurveEndGeo = endGeo;
                SelectedPrefabBase = prefabBase;
                return true;
            }

            /// <inheritdoc/>
            public void Reset() {
                SelectedEdgeEntity = Entity.Null;
                SelectedCurve = default;
                SelectedCurveGeo = default;
                SelectedCurveEdgeGeo = default;
                SelectedPrefabBase = null;
            }

            public class CurveData {
                public Bezier4x3 Curve;
                public float3 StartPointLocation;
                public float3 EndPointLocation;
                public float Length;
                public float StartingPoint = 0;
                public float Direction = 1;

                public CurveData(Bezier4x3 curve, float direction) {
                    Curve = curve;
                    Length = MathUtils.Length(curve);
                    Direction = direction;
                    StartPointLocation = MathUtils.Position(
                        Curve,
                        0f
                    );
                    EndPointLocation = MathUtils.Position(
                        Curve,
                        1f
                    );
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
                OverlayRenderSystem.Buffer overlayBuffer,
                bool debug
            ) {
                // Make sure there's always a little offset, for directional vector calc
                offset += 0.1f;

                // Don't do anything if we don't have a valid start point.
                if (SelectedEdgeEntity == Entity.Null) {
                    return;
                }

                // Curves
                var curvesDict = new Dictionary<string, List<CurveData>> {
                    { "left", new List<CurveData>() },
                    { "right", new List<CurveData>() },
                    { "start", new List<CurveData>() },
                    { "end", new List<CurveData>() }
                };

                // The specific order in which these curves are added to each list is important,
                // as we're trying to create a continuous curve to calculate points on
                if (sides.x) {
                    curvesDict["left"].Add(new CurveData(SelectedCurveEdgeGeo.m_Start.m_Left, -1f));
                    curvesDict["left"].Add(new CurveData(SelectedCurveEdgeGeo.m_End.m_Left, -1f));
                }

                if (sides.y) {
                    curvesDict["right"].Add(new CurveData(SelectedCurveEdgeGeo.m_Start.m_Right, 1f));
                    curvesDict["right"].Add(new CurveData(SelectedCurveEdgeGeo.m_End.m_Right, 1f));
                }

                if (sides.z) {
                    curvesDict["start"].Add(new CurveData(SelectedCurveStartGeo.m_Geometry.m_Left.m_Right, 1f));
                    curvesDict["start"].Add(new CurveData(SelectedCurveStartGeo.m_Geometry.m_Right.m_Right, 1f));
                    curvesDict["start"].Add(new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveStartGeo.m_Geometry.m_Right.m_Left), -1f));
                    curvesDict["start"].Add(new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveStartGeo.m_Geometry.m_Left.m_Left), -1f));
                }

                if (sides.w) {
                    curvesDict["end"].Add(new CurveData(SelectedCurveEndGeo.m_Geometry.m_Left.m_Left, -1f));
                    curvesDict["end"].Add(new CurveData(SelectedCurveEndGeo.m_Geometry.m_Right.m_Left, -1f));
                    curvesDict["end"].Add(new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveEndGeo.m_Geometry.m_Right.m_Right), 1f));
                    curvesDict["end"].Add(new CurveData(PlatterMathUtils.InvertBezier(SelectedCurveEndGeo.m_Geometry.m_Left.m_Right), 1f));
                }

                foreach (var curves in curvesDict.Values) {
                    if (curves.Count == 0) {
                        continue;
                    }

                    if (debug) {
                        var debugLinesColor = 0;
                        foreach (var curve in curves) {
                            overlayBuffer.DrawCircle(DebugColors[debugLinesColor], new Color(1f, 1f, 1f, 1f), 0.2f, OverlayRenderSystem.StyleFlags.Grid, new float2(1, 1), curve.StartPointLocation, 3f);
                            overlayBuffer.DrawCircle(DebugColors[debugLinesColor], new Color(0f, 0f, 0f, 1f), 0.2f, OverlayRenderSystem.StyleFlags.Grid, new float2(1, 1), curve.EndPointLocation, 3f);
                            overlayBuffer.DrawCurve(DebugColors[debugLinesColor], curve.Curve, 1f);
                            debugLinesColor = (debugLinesColor < DebugColors.Length - 1) ? debugLinesColor + 1 : 0;
                        }
                    }

                    var totalLength = 0f;

                    for (var i = 0; i < curves.Count; i++) {
                        var curve = curves[i];
                        if (curves.ElementAtOrDefault(i - 1) != null) {
                            curve.StartingPoint = curves[i - 1].StartingPoint + curves[i - 1].Length;
                        }

                        totalLength += curve.Length;
                    }

                    var pointsCount = math.floor((totalLength + spacing) / (size + spacing));
                    var intervals = pointsCount - 1;
                    var stepLength = intervals == 0 ? 0 : totalLength / intervals;

                    // Generate points, retrieving the correct position on the correct curve.
                    var currentCurveIndex = 0;
                    var debugPointsColor = 0;

                    for (var i = 0; i < pointsCount; i++) {
                        var currentPosition = i * stepLength;

                        // Get the right curve given the currentPosition
                        if (currentPosition > curves[currentCurveIndex].StartingPoint + curves[currentCurveIndex].Length && currentCurveIndex < curves.Count - 1) {
                            currentCurveIndex++;
                        }

                        var currentCurve = curves[currentCurveIndex];
                        var relativePosition = currentPosition - currentCurve.StartingPoint;
                        var relativePercentage = relativePosition / currentCurve.Length;
                        var pointPositionOnCurve = MathUtils.Position(
                            currentCurve.Curve,
                            relativePercentage
                        );
                        var tangent = MathUtils.Tangent(currentCurve.Curve, relativePercentage);
                        var perpendicular2dVector = new Vector3(tangent.z, 0f, -tangent.x);
                        float3 normal = perpendicular2dVector.normalized;
                        var shiftedPointPosition = pointPositionOnCurve + (normal * offset * currentCurve.Direction);
                        Vector3 direction = pointPositionOnCurve - shiftedPointPosition;
                        var perpendicularRotation = Quaternion.LookRotation(direction.normalized);

                        if (debug) {
                            overlayBuffer.DrawCircle(DebugColors[debugPointsColor], shiftedPointPosition, 3f);
                            debugPointsColor = (debugPointsColor < DebugColors.Length - 1) ? debugPointsColor + 1 : 0;
                        }

                        pointList.Add(new Transform {
                            m_Position = shiftedPointPosition,
                            m_Rotation = perpendicularRotation,
                        });
                    }
                }
            }
        }
    }
}
