// <copyright file="ColorConstants.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Constants {
    #region Using Statements

    using UnityEngine;

    #endregion

    public static class ColorConstants {
        public static readonly Color[] DebugColors = {
            Color.blue,
            Color.red,
            Color.green,
            Color.yellow,
            Color.white,
            Color.black,
            Color.cyan,
            Color.magenta,
        };

        // Guideline ColorConstants
        public static readonly Color GuidelinePrimary   = Color.cyan;
        public static readonly Color GuidelineSecondary = Color.magenta;

        public static readonly float OpacityFull = 1f;

        // ColorConstants (general)
        public static readonly float OpacityLow           = 0.2f;
        public static readonly float OpacityMedium        = 0.4f;
        public static readonly Color ParcelBackground     = new(255f / 255f, 255f / 255f, 255f / 255f, OpacityMedium);
        public static readonly Color ParcelCellOutline    = Color.grey;
        public static readonly Color ParcelFrontIndicator = Color.white;
        public static readonly Color ParcelInline         = new(255f / 255f, 255f / 255f, 255f / 255f, OpacityLow);

        // Parcel ColorConstants
        public static readonly Color ParcelOutline = new(255f / 255f, 255f / 255f, 255f / 255f, OpacityMedium);
    }
}