// <copyright file="MathUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using Colossal.Mathematics;

    public class MathUtils {
        public static Bezier4x3 InvertBezier(Bezier4x3 curve) {
            return new Bezier4x3(curve.d, curve.c, curve.b, curve.a);
        }
    }
}