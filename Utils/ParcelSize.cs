// <copyright file="ParcelSize.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using Platter.Constants;
    using Unity.Mathematics;

    public class ParcelSize {
        private float3 size;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelSize"/> class.
        /// </summary>
        /// <param name="size"></param>
        public ParcelSize(float3 size) {
            this.size = size;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelSize"/> class.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="depth"></param>
        /// <param name="height"></param>
        public ParcelSize(float width, float depth, float height) {
            this.size = default;
            size.x = width;
            size.z = depth;
            size.y = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ParcelSize"/> class.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="depth"></param>
        public ParcelSize(float width, float depth) {
            this.size = default;
            size.x = width;
            size.z = depth;
            size.y = DimensionConstants.ParcelHeight;
        }

        public float Width => size.x;

        public float Depth => size.z;

        public float Height => size.y;

        public static implicit operator ParcelSize(float3 size) {
            return new ParcelSize(size);
        }
    }
}
