// <copyright file="ParcelUtilsTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests.Utils {
    using System;
    using Platter.Components;
    using Platter.Constants;
    using Platter.Utils;
    using Unity.Mathematics;

    /// <summary>
    /// Unit tests for <see cref="ParcelUtils"/> static utility methods.
    /// These tests verify prefab ID generation and backward-compatible forwarding methods.
    /// </summary>
    /// <remarks>
    /// Tests for geometry methods (NodeMult, GetTransformMatrix, GetWorldPosition) are in
    /// <see cref="ParcelGeometryUtilsTests"/> since those methods are now in <see cref="ParcelGeometryUtils"/>.
    /// Tests requiring Unity runtime (e.g., GetCustomHashCode which uses Hash128.Compute)
    /// should be placed in the in-game TestScenario tests instead.
    /// </remarks>
    [TestFixture]
    public class ParcelUtilsTests {
        #region GetPrefabID Tests

        [Test]
        public void GetPrefabID_WithWidthAndDepth_ReturnsCorrectPrefabID() {
            var prefabId = ParcelUtils.GetPrefabID(2, 3);

            Assert.That(prefabId.GetName(), Is.EqualTo("Parcel 2x3"));
        }

        [Test]
        public void GetPrefabID_WithDifferentSizes_ReturnsCorrectNames() {
            var prefab1x2 = ParcelUtils.GetPrefabID(1, 2);
            var prefab4x6 = ParcelUtils.GetPrefabID(4, 6);
            var prefab6x6 = ParcelUtils.GetPrefabID(6, 6);

            Assert.That(prefab1x2.GetName(), Is.EqualTo("Parcel 1x2"));
            Assert.That(prefab4x6.GetName(), Is.EqualTo("Parcel 4x6"));
            Assert.That(prefab6x6.GetName(), Is.EqualTo("Parcel 6x6"));
        }

        [Test]
        public void GetPrefabID_WithInt2Size_ReturnsCorrectPrefabID() {
            var size     = new int2(3, 4);
            var prefabId = ParcelUtils.GetPrefabID(size);

            Assert.That(prefabId.GetName(), Is.EqualTo("Parcel 3x4"));
        }

        #endregion

        #region GetParcelSize (ParcelData overload) Tests

        [Test]
        public void GetParcelSize_WithParcelData_ReturnsCorrectDimensions() {
            var parcelData = new ParcelData { m_LotSize = new int2(2, 3) };

#pragma warning disable CS0618 // Type or member is obsolete
            var result = ParcelUtils.GetParcelSize(parcelData);
#pragma warning restore CS0618

            Assert.That(result.x, Is.EqualTo(2 * DimensionConstants.CellSize));
            Assert.That(result.y, Is.EqualTo(DimensionConstants.ParcelHeight));
            Assert.That(result.z, Is.EqualTo(3 * DimensionConstants.CellSize));
        }

        #endregion

        #region Backward Compatibility Tests

        [Test]
        public void ParcelNode_Enum_MatchesParcelGeometryUtilsEnum() {
#pragma warning disable CS0618 // Type or member is obsolete
            // Verify the enum values match between the two locations
            Assert.That((int)ParcelUtils.ParcelNode.CornerLeftFront, Is.EqualTo((int)ParcelGeometryUtils.ParcelNode.CornerLeftFront));
            Assert.That((int)ParcelUtils.ParcelNode.CornerRightBack, Is.EqualTo((int)ParcelGeometryUtils.ParcelNode.CornerRightBack));
            Assert.That((int)ParcelUtils.ParcelNode.FrontAccess, Is.EqualTo((int)ParcelGeometryUtils.ParcelNode.FrontAccess));
#pragma warning restore CS0618
        }

        [Test]
        public void NodeMult_ForwardsToParcelGeometryUtils() {
#pragma warning disable CS0618 // Type or member is obsolete
            var resultFromParcelUtils   = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftFront);
#pragma warning restore CS0618
            var resultFromGeometryUtils = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerLeftFront);

            Assert.That(resultFromParcelUtils.x, Is.EqualTo(resultFromGeometryUtils.x));
            Assert.That(resultFromParcelUtils.y, Is.EqualTo(resultFromGeometryUtils.y));
            Assert.That(resultFromParcelUtils.z, Is.EqualTo(resultFromGeometryUtils.z));
        }

        [Test]
        public void GetTransformMatrix_ForwardsToParcelGeometryUtils() {
            var rotation = quaternion.RotateY(0.5f);
            var position = new float3(10f, 20f, 30f);

#pragma warning disable CS0618 // Type or member is obsolete
            var resultFromParcelUtils   = ParcelUtils.GetTransformMatrix(rotation, position);
#pragma warning restore CS0618
            var resultFromGeometryUtils = ParcelGeometryUtils.GetTransformMatrix(rotation, position);

            Assert.That(resultFromParcelUtils.c3.x, Is.EqualTo(resultFromGeometryUtils.c3.x));
            Assert.That(resultFromParcelUtils.c3.y, Is.EqualTo(resultFromGeometryUtils.c3.y));
            Assert.That(resultFromParcelUtils.c3.z, Is.EqualTo(resultFromGeometryUtils.c3.z));
        }

        [Test]
        public void GetWorldPosition_ForwardsToParcelGeometryUtils() {
            var rotation = quaternion.identity;
            var position = new float3(100f, 0f, 100f);
            var trs      = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(5f, 0f, 5f);

#pragma warning disable CS0618 // Type or member is obsolete
            var resultFromParcelUtils   = ParcelUtils.GetWorldPosition(trs, center, float3.zero);
#pragma warning restore CS0618
            var resultFromGeometryUtils = ParcelGeometryUtils.GetWorldPosition(trs, center, float3.zero);

            Assert.That(resultFromParcelUtils.x, Is.EqualTo(resultFromGeometryUtils.x));
            Assert.That(resultFromParcelUtils.y, Is.EqualTo(resultFromGeometryUtils.y));
            Assert.That(resultFromParcelUtils.z, Is.EqualTo(resultFromGeometryUtils.z));
        }

        #endregion
    }
}
