﻿// <copyright file="DimensionConstants.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Constants {
    public static class DimensionConstants {
        // General
        public static readonly float StandardLineWidth = 0.2f;

        // Parcels & Blocks
        public static readonly float CellSize = 8f;
        public static readonly float ParcelHeight = 1f;

        // Parcel Overlays
        public static readonly float ParcelOutlineWidth = StandardLineWidth * 2;
        public static readonly float ParcelFrontIndicatorHollowLineWidth = StandardLineWidth;
        public static readonly float ParcelFrontIndicatorDiameter = 3f;
        public static readonly float ParcelCellOutlineWidth = StandardLineWidth;

        // Tool Guidelines
        public static readonly float GuidelinePrimaryWidth = StandardLineWidth;
        public static readonly float GuidelineSecondaryWidth = StandardLineWidth;
    }
}
