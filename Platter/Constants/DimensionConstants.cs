// <copyright file="DimensionConstants.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Constants {
    public static class DimensionConstants {
        // Parcels & Blocks
        public const float CellSize = 8f;

        // Tool Guidelines
        public const float StandardLineWidth                   = 0.2f;
        public const float ParcelCellOutlineWidth              = StandardLineWidth;
        public const float ParcelFrontIndicatorDiameter        = 1f;
        public const float ParcelFrontIndicatorHollowLineWidth = StandardLineWidth;
        public const float ParcelHeight                        = 1f;

        // Parcel Overlays
        public const float ParcelOutlineWidth = 0.2f;
    }
}