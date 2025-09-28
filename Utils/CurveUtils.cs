// <copyright file="CurveUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using Colossal.Mathematics;
    using Unity.Mathematics;
    using UnityEngine;

    internal class CurveUtils {
        /// <summary>
        /// Steps along a Bezier calculating the target t factor for the given starting t factor and the given distance.
        /// Code based on Alterran's PropLineTool (StepDistanceCurve, Utilities/PLTMath.cs).
        /// </summary>
        /// <param name="tStart">Starting t factor.</param>
        /// <param name="distance">Distance to travel.</param>
        /// <returns>Target t factor.</returns>
        public static float BezierStep(Bezier4x3 bezier, float tStart, float distance) {
            const float Tolerance = 0.001f;
            const float ToleranceSquared = Tolerance * Tolerance;

            var tEnd = Travel(bezier, tStart, distance);
            var usedDistance = CubicBezierArcLengthXZGauss04(bezier, tStart, tEnd);

            // Twelve iteration maximum for performance and to prevent infinite loops.
            for (var i = 0; i < 12; ++i) {
                // Stop looping if the remaining distance is less than tolerance.
                var remainingDistance = distance - usedDistance;
                if (remainingDistance * remainingDistance < ToleranceSquared) {
                    break;
                }

                usedDistance = CubicBezierArcLengthXZGauss04(bezier, tStart, tEnd);
                tEnd += (distance - usedDistance) / CubicSpeedXZ(bezier, tEnd);
            }

            return tEnd;
        }

        /// <summary>
        /// Steps along a Bezier BACKWARDS from the given t factor, calculating the target t factor for the given spacing distance.
        /// Code based on Alterran's PropLineTool (StepDistanceCurve, Utilities/PLTMath.cs).
        /// </summary>
        /// <param name="tStart">Starting t factor.</param>
        /// <param name="distance">Distance to travel.</param>
        /// <returns>Target t factor.</returns>
        public static float BezierStepReverse(Bezier4x3 bezier, float tStart, float distance) {
            const float Tolerance = 0.001f;
            const float ToleranceSquared = Tolerance * Tolerance;

            var tEnd = Travel(bezier, tStart, -distance);
            var usedDistance = CubicBezierArcLengthXZGauss04(bezier, tEnd, tStart);

            // Twelve iteration maximum for performance and to prevent infinite loops.
            for (var i = 0; i < 12; ++i) {
                // Stop looping if the remaining distance is less than tolerance.
                var remainingDistance = distance - usedDistance;
                if (remainingDistance * remainingDistance < ToleranceSquared) {
                    break;
                }

                usedDistance = CubicBezierArcLengthXZGauss04(bezier, tEnd, tStart);
                tEnd -= (distance - usedDistance) / CubicSpeedXZ(bezier, tEnd);
            }

            return tEnd;
        }

        /// <summary>
        /// From Alterann's PropLineTool (CubicSpeedXZ, Utilities/PLTMath.cs).
        /// Returns the integrand of the arc length function for a cubic Bezier curve, constrained to the XZ-plane at a specific t.
        /// </summary>
        /// <param name="t"> t factor.</param>
        /// <returns>Integrand of arc length.</returns>
        public static float CubicSpeedXZ(Bezier4x3 bezier, float t) {
            // Pythagorean theorem.
            var tangent = MathUtils.Tangent(bezier, t);
            var derivXsqr = tangent.x * tangent.x;
            var derivZsqr = tangent.z * tangent.z;

            return math.sqrt(derivXsqr + derivZsqr);
        }

        /// <summary>
        /// From Alterann's PropLineTool (CubicBezierArcLengthXZGauss04, Utilities/PLTMath.cs).
        /// Returns the XZ arclength of a cubic Bezier curve between two t factors.
        /// Uses Gauss–Legendre Quadrature with n = 4.
        /// </summary>
        /// <param name="t1">Starting t factor.</param>
        /// <param name="t2">Ending t factor.</param>
        /// <returns>XZ arc length.</returns>
        public static float CubicBezierArcLengthXZGauss04(Bezier4x3 bezier, float t1, float t2) {
            var linearAdj = (t2 - t1) / 2f;

            // Constants are from Gauss-Lengendre quadrature rules for n = 4.
            var p1 = CubicSpeedXZGaussPoint(bezier, .3399810435848563f, 0.6521451548625461f, t1, t2);
            var p2 = CubicSpeedXZGaussPoint(bezier, 0.3399810435848563f, 0.6521451548625461f, t1, t2);
            var p3 = CubicSpeedXZGaussPoint(bezier, .8611363115940526f, 0.3478548451374538f, t1, t2);
            var p4 = CubicSpeedXZGaussPoint(bezier, -0.8611363115940526f, 0.3478548451374538f, t1, t2);

            return linearAdj * (p1 + p2 + p3 + p4);
        }

        /// <summary>
        /// From Alterann's PropLineTool (CubicSpeedXZGaussPoint, Utilities/PLTMath.cs).
        /// </summary>
        /// <param name="x_i">X i.</param>
        /// <param name="w_i">W i.</param>
        /// <param name="a">a.</param>
        /// <param name="b">b.</param>
        /// <returns>Cubic speed.</returns>
        public static float CubicSpeedXZGaussPoint(Bezier4x3 bezier, float x_i, float w_i, float a, float b) {
            var linearAdj = (b - a) / 2f;
            var constantAdj = (a + b) / 2f;
            return w_i * CubicSpeedXZ(bezier, (linearAdj * x_i) + constantAdj);
        }

        /// <summary>
        /// Based on CS1's mathematics calculations for Bezier travel.
        /// </summary>
        /// <param name="start">Starting t-factor.</param>
        /// <param name="distance">Distance to travel.</param>
        /// <returns>Ending t-factor.</returns>
        public static float Travel(Bezier4x3 bezier, float start, float distance) {
            Vector3 startPos = MathUtils.Position(bezier, start);

            if (distance < 0f) {
                // Negative (reverse) direction.
                distance = 0f - distance;
                var startT = 0f;
                var endT = start;
                var startDistance = Vector3.SqrMagnitude(bezier.a - (float3)startPos);
                var endDistance = 0f;

                // Eight steps max.
                for (var i = 0; i < 8; ++i) {
                    // Calculate current position.
                    var midT = (startT + endT) * 0.5f;
                    Vector3 midpoint = MathUtils.Position(bezier, midT);
                    var midDistance = Vector3.SqrMagnitude(midpoint - startPos);

                    // Check for nearer match.
                    if (midDistance < distance * distance) {
                        endT = midT;
                        endDistance = midDistance;
                    } else {
                        startT = midT;
                        startDistance = midDistance;
                    }
                }

                // We've been using square magnitudes for comparison, so rest to true value.
                startDistance = Mathf.Sqrt(startDistance);
                endDistance = Mathf.Sqrt(endDistance);

                // Check for exact match.
                var fDiff = startDistance - endDistance;
                if (fDiff == 0f) {
                    // Exact match found - return that.
                    return endT;
                }

                // Not an exact match - use an interpolation.
                return Mathf.Lerp(endT, startT, Mathf.Clamp01((distance - endDistance) / fDiff));
            } else {
                // Positive (forward) direction.
                var startT = start;
                var endT = 1f;
                var startDistance = 0f;
                var endDistance = Vector3.SqrMagnitude(bezier.d - (float3)startPos);

                // Eight steps max.
                for (var i = 0; i < 8; ++i) {
                    // Calculate current position.
                    var tMid = (startT + endT) * 0.5f;
                    Vector3 midPoint = MathUtils.Position(bezier, tMid);
                    var midDistance = Vector3.SqrMagnitude(midPoint - startPos);

                    // Check for nearer match.
                    if (midDistance < distance * distance) {
                        startT = tMid;
                        startDistance = midDistance;
                    } else {
                        endT = tMid;
                        endDistance = midDistance;
                    }
                }

                // We've been using square magnitudes for comparison, so rest to true value.
                startDistance = Mathf.Sqrt(startDistance);
                endDistance = Mathf.Sqrt(endDistance);

                // Check for exact match.
                var remainder = endDistance - startDistance;
                if (remainder == 0f) {
                    // Exact match found - return that.
                    return startT;
                }

                // Not an exact match - use an interpolation.
                return Mathf.Lerp(startT, endT, Mathf.Clamp01((distance - startDistance) / remainder));
            }
        }
    }
}
