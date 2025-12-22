// <copyright file="ParcelGeometryUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using Colossal.Mathematics;
    using Constants;
    using Unity.Mathematics;
    using Transform = Game.Objects.Transform;

    #endregion

    /// <summary>
    /// Burst-compatible static utility class for parcel geometry calculations.
    /// This replaces the managed class <see cref="ParcelGeometry"/> for use in burst-compiled jobs.
    /// </summary>
    public static class ParcelGeometryUtils {
        /// <summary>
        /// Calculates the parcel size from lot dimensions.
        /// </summary>
        /// <param name="lotSize">Lot size in cells (width x depth).</param>
        /// <returns>Parcel size as a float3 (width, height, depth).</returns>
        public static float3 GetParcelSize(int2 lotSize) {
            return new float3(
                lotSize.x * DimensionConstants.CellSize,
                DimensionConstants.ParcelHeight,
                lotSize.y * DimensionConstants.CellSize
            );
        }

        /// <summary>
        /// Calculates the block size from lot dimensions, applying minimum constraints.
        /// </summary>
        /// <param name="lotSize">Lot size in cells (width x depth).</param>
        /// <returns>Block size as a float3 (width, height, depth).</returns>
        public static float3 GetBlockSize(int2 lotSize) {
            return new float3(
                math.max(2, lotSize.x) * DimensionConstants.CellSize,
                DimensionConstants.ParcelHeight,
                6 * DimensionConstants.CellSize
            );
        }

        /// <summary>
        /// Calculates the parcel bounds from parcel size.
        /// </summary>
        /// <param name="parcelSize">Parcel size.</param>
        /// <returns>Bounds3 representing the parcel bounds.</returns>
        public static Bounds3 GetParcelBounds(float3 parcelSize) {
            return new Bounds3(
                new float3(-parcelSize.x / 2, -parcelSize.y / 2, -parcelSize.z / 2),
                new float3(parcelSize.x  / 2, parcelSize.y  / 2, parcelSize.z  / 2)
            );
        }

        /// <summary>
        /// Calculates the block bounds from parcel and block sizes.
        /// </summary>
        /// <param name="parcelSize">Parcel size.</param>
        /// <param name="blockSize">Block size.</param>
        /// <returns>Bounds3 representing the block bounds.</returns>
        public static Bounds3 GetBlockBounds(float3 parcelSize, float3 blockSize) {
            var shiftX = (blockSize.x - parcelSize.x) / 2;
            var shiftZ = (blockSize.z - parcelSize.z) / 2;
            return new Bounds3(
                new float3(-blockSize.x / 2 - shiftX, -blockSize.y / 2, -blockSize.z / 2 - shiftZ),
                new float3(blockSize.x  / 2 - shiftX, blockSize.y  / 2, blockSize.z  / 2 - shiftZ)
            );
        }

        /// <summary>
        /// Calculates the center point of bounds.
        /// </summary>
        /// <param name="bounds">The bounds to calculate center for.</param>
        /// <returns>The center point of the bounds.</returns>
        public static float3 GetCenter(Bounds3 bounds) { return MathUtils.Center(bounds); }

        /// <summary>
        /// Calculates the block center from lot size.
        /// This is the primary method used in burst-compiled jobs.
        /// </summary>
        /// <param name="lotSize">Lot size in cells (width x depth).</param>
        /// <returns>The center point of the block.</returns>
        public static float3 GetBlockCenter(int2 lotSize) {
            var parcelSize  = GetParcelSize(lotSize);
            var blockSize   = GetBlockSize(lotSize);
            var blockBounds = GetBlockBounds(parcelSize, blockSize);
            return GetCenter(blockBounds);
        }

        /// <summary>
        /// Calculates the parcel center from lot size.
        /// </summary>
        /// <param name="lotSize">Lot size in cells (width x depth).</param>
        /// <returns>The center point of the parcel.</returns>
        public static float3 GetParcelCenter(int2 lotSize) {
            var parcelSize   = GetParcelSize(lotSize);
            var parcelBounds = GetParcelBounds(parcelSize);
            return GetCenter(parcelBounds);
        }


        /// <summary>
        /// Calculates world-space corner positions for a parcel from lot size.
        /// </summary>
        /// <param name="transform">The parcel's transform.</param>
        /// <param name="lotSize">Lot size in cells (width x depth).</param>
        /// <returns>float3x4 with corners: c0=LeftFront, c1=RightFront, c2=RightBack, c3=LeftBack.</returns>
        public static Quad2 GetWorldCorners(Transform transform, int2 lotSize) {
            var trs = ParcelUtils.GetTransformMatrix(transform);

            return GetWorldCorners(trs, GetParcelSize(lotSize));
        }

        public static Quad2 GetWorldCorners(quaternion rotation, float3 position, int2 lotSize) {
            var trs = ParcelUtils.GetTransformMatrix(rotation, position);

            return GetWorldCorners(trs, GetParcelSize(lotSize));
        }

        /// <summary>
        /// Calculates world-space corner positions for a parcel.
        /// </summary>
        /// <param name="trs">The parcel's transform matrix.</param>
        /// <param name="parcelSize">The parcel size (width, height, depth).</param>
        /// <returns>
        /// float3x4 with corners: c0=RightFront, c1=LeftFront, c2=LeftBack, c3=RightBack.
        /// Corner layout (looking from above, front = +Z direction after rotation):
        ///   LeftBack ───── RightBack
        ///      │              │
        ///      │   (center)   │
        ///      │              │
        ///  LeftFront ─── RightFront (front edge faces road)
        /// </returns>
        public static Quad2 GetWorldCorners(float4x4 trs, float3 parcelSize) {
            // Local space corners using ParcelUtils.NodeMult pattern
            var localRightFront = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightFront) * parcelSize;
            var localLeftFront  = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftFront) * parcelSize;
            var localLeftBack   = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftBack) * parcelSize;
            var localRightBack  = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightBack) * parcelSize;

            // Transform to world space
            return new Quad2(
                math.transform(trs, localRightFront).xz,
                math.transform(trs, localLeftFront).xz,
                math.transform(trs, localLeftBack).xz,
                math.transform(trs, localRightBack).xz
            );
        }
    }
}