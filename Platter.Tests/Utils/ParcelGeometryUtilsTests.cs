// <copyright file="ParcelGeometryUtilsTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests.Utils {
    using Platter.Constants;
    using Platter.Utils;
    using Unity.Mathematics;

    /// <summary>
    /// Unit tests for <see cref="ParcelGeometryUtils"/> static utility methods.
    /// These tests verify geometry calculations without requiring Unity runtime.
    /// </summary>
    [TestFixture]
    public class ParcelGeometryUtilsTests {
        private const float Tolerance = 0.0001f;

        #region NodeMult Tests

        [TestCase(ParcelGeometryUtils.ParcelNode.CornerLeftFront, 0.5f, 0f, 0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.CornerRightFront, -0.5f, 0f, 0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.CornerLeftBack, 0.5f, 0f, -0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.CornerRightBack, -0.5f, 0f, -0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.FrontAccess, 0f, 0f, 0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.BackAccess, 0f, 0f, -0.5f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.LeftAccess, 0.5f, 0f, 0f)]
        [TestCase(ParcelGeometryUtils.ParcelNode.RightAccess, -0.5f, 0f, 0f)]
        [TestCase((ParcelGeometryUtils.ParcelNode)999, 0f, 0f, 0f)]
        public void NodeMult_ReturnsCorrectMultiplier(ParcelGeometryUtils.ParcelNode node, float expectedX, float expectedY, float expectedZ) {
            var result = ParcelGeometryUtils.NodeMult(node);

            Assert.That(result.x, Is.EqualTo(expectedX));
            Assert.That(result.y, Is.EqualTo(expectedY));
            Assert.That(result.z, Is.EqualTo(expectedZ));
        }

        [Test]
        public void NodeMult_MultipliedByParcelSize_ReturnsCorrectCornerPosition() {
            // A 2x3 parcel has size: 2*8=16 width, 3*8=24 depth
            var parcelSize = new float3(16f, DimensionConstants.ParcelHeight, 24f);
            var nodeMult   = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerLeftFront);

            var cornerPosition = nodeMult * parcelSize;

            // CornerLeftFront is at (0.5, 0, 0.5) * size = (8, 0, 12)
            Assert.That(cornerPosition.x, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(cornerPosition.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(cornerPosition.z, Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void NodeMult_AllCorners_FormRectangle() {
            var parcelSize = new float3(16f, 1f, 24f); // 2x3 lot

            var leftFront  = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerLeftFront) * parcelSize;
            var rightFront = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerRightFront) * parcelSize;
            var leftBack   = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerLeftBack) * parcelSize;
            var rightBack  = ParcelGeometryUtils.NodeMult(ParcelGeometryUtils.ParcelNode.CornerRightBack) * parcelSize;

            // Width should be consistent (leftFront.x - rightFront.x = 16)
            Assert.That(leftFront.x - rightFront.x, Is.EqualTo(16f).Within(Tolerance));
            Assert.That(leftBack.x - rightBack.x, Is.EqualTo(16f).Within(Tolerance));

            // Depth should be consistent (leftFront.z - leftBack.z = 24)
            Assert.That(leftFront.z - leftBack.z, Is.EqualTo(24f).Within(Tolerance));
            Assert.That(rightFront.z - rightBack.z, Is.EqualTo(24f).Within(Tolerance));
        }

        #endregion

        #region GetTransformMatrix Tests

        [Test]
        public void GetTransformMatrix_IdentityRotationAtOrigin_ReturnsIdentityMatrix() {
            var rotation = quaternion.identity;
            var position = float3.zero;

            var result = ParcelGeometryUtils.GetTransformMatrix(rotation, position);

            // Check that the translation part is at origin
            Assert.That(result.c3.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.c3.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.c3.z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void GetTransformMatrix_WithPosition_ReturnsCorrectTranslation() {
            var rotation = quaternion.identity;
            var position = new float3(10f, 20f, 30f);

            var result = ParcelGeometryUtils.GetTransformMatrix(rotation, position);

            Assert.That(result.c3.x, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(result.c3.y, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(result.c3.z, Is.EqualTo(30f).Within(Tolerance));
        }

        #endregion

        #region GetWorldPosition Tests

        [Test]
        public void GetWorldPosition_AtOriginWithNoOffset_ReturnsCenter() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var trs      = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(5f, 0f, 5f);

            var result = ParcelGeometryUtils.GetWorldPosition(trs, center, float3.zero);

            Assert.That(result.x, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(5f).Within(Tolerance));
        }

        [Test]
        public void GetWorldPosition_WithOffset_AddsOffsetToCenter() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var trs      = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(0f, 0f, 0f);
            var offset   = new float3(1f, 2f, 3f);

            var result = ParcelGeometryUtils.GetWorldPosition(trs, center, offset);

            Assert.That(result.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(3f).Within(Tolerance));
        }

        [Test]
        public void GetWorldPosition_WithTranslation_TranslatesResult() {
            var rotation = quaternion.identity;
            var position = new float3(100f, 0f, 100f);
            var trs      = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(5f, 0f, 5f);

            var result = ParcelGeometryUtils.GetWorldPosition(trs, center, float3.zero);

            Assert.That(result.x, Is.EqualTo(105f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(105f).Within(Tolerance));
        }

        [Test]
        public void GetWorldPosition_With90DegreeRotation_RotatesCorrectly() {
            // 90 degrees around Y axis
            var rotation = quaternion.RotateY(math.PI / 2f);
            var position = float3.zero;
            var trs      = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(10f, 0f, 0f); // 10 units in X direction

            var result = ParcelGeometryUtils.GetWorldPosition(trs, center, float3.zero);

            // After 90 degree rotation around Y, X becomes -Z
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(-10f).Within(Tolerance));
        }

        #endregion

        #region GetParcelSize Tests

        [Test]
        public void GetParcelSize_WithLotSize_ReturnsCorrectDimensions() {
            var lotSize = new int2(2, 3);

            var result = ParcelGeometryUtils.GetParcelSize(lotSize);

            Assert.That(result.x, Is.EqualTo(16f)); // 2 * 8
            Assert.That(result.y, Is.EqualTo(DimensionConstants.ParcelHeight));
            Assert.That(result.z, Is.EqualTo(24f)); // 3 * 8
        }

        [Test]
        public void GetParcelSize_WithSquareLot_ReturnsSquareParcel() {
            var lotSize = new int2(4, 4);

            var result = ParcelGeometryUtils.GetParcelSize(lotSize);

            Assert.That(result.x, Is.EqualTo(result.z));
            Assert.That(result.x, Is.EqualTo(32f)); // 4 * 8
        }

        [TestCase(1, 1, 8f, 8f)]
        [TestCase(2, 4, 16f, 32f)]
        [TestCase(6, 6, 48f, 48f)]
        public void GetParcelSize_VariousSizes_ReturnsCorrectDimensions(int width, int depth, float expectedX, float expectedZ) {
            var lotSize = new int2(width, depth);

            var result = ParcelGeometryUtils.GetParcelSize(lotSize);

            Assert.That(result.x, Is.EqualTo(expectedX));
            Assert.That(result.z, Is.EqualTo(expectedZ));
        }

        #endregion

        #region GetBlockSize Tests

        [Test]
        public void GetBlockSize_WithLotSize_ReturnsCorrectDimensions() {
            var lotSize = new int2(3, 4);

            var result = ParcelGeometryUtils.GetBlockSize(lotSize);

            Assert.That(result.x, Is.EqualTo(24f)); // 3 * 8
            Assert.That(result.y, Is.EqualTo(DimensionConstants.ParcelHeight));
            Assert.That(result.z, Is.EqualTo(48f)); // Fixed 6 * 8
        }

        [Test]
        public void GetBlockSize_DepthIsAlwaysSixCells() {
            var lotSize1 = new int2(2, 2);
            var lotSize2 = new int2(4, 8);

            var result1 = ParcelGeometryUtils.GetBlockSize(lotSize1);
            var result2 = ParcelGeometryUtils.GetBlockSize(lotSize2);

            Assert.That(result1.z, Is.EqualTo(48f)); // 6 * 8
            Assert.That(result2.z, Is.EqualTo(48f)); // 6 * 8
        }

        [Test]
        public void GetBlockSize_WidthLessThanTwo_AppliesMinimumConstraint() {
            var lotSize = new int2(1, 3);

            var result = ParcelGeometryUtils.GetBlockSize(lotSize);

            Assert.That(result.x, Is.EqualTo(16f)); // min(2, 1) = 2, so 2 * 8
        }

        [Test]
        public void GetBlockSize_WidthGreaterThanTwo_UsesActualWidth() {
            var lotSize = new int2(5, 3);

            var result = ParcelGeometryUtils.GetBlockSize(lotSize);

            Assert.That(result.x, Is.EqualTo(40f)); // 5 * 8
        }

        #endregion

        #region GetParcelBounds Tests

        [Test]
        public void GetParcelBounds_ReturnsCenteredBounds() {
            var parcelSize = new float3(16f, 1f, 24f);

            var result = ParcelGeometryUtils.GetParcelBounds(parcelSize);

            Assert.That(result.min.x, Is.EqualTo(-8f).Within(Tolerance));
            Assert.That(result.min.y, Is.EqualTo(-0.5f).Within(Tolerance));
            Assert.That(result.min.z, Is.EqualTo(-12f).Within(Tolerance));
            Assert.That(result.max.x, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(result.max.y, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(result.max.z, Is.EqualTo(12f).Within(Tolerance));
        }

        [Test]
        public void GetParcelBounds_IsSymmetric() {
            var parcelSize = new float3(20f, 2f, 30f);

            var result = ParcelGeometryUtils.GetParcelBounds(parcelSize);

            Assert.That(result.min.x, Is.EqualTo(-result.max.x).Within(Tolerance));
            Assert.That(result.min.y, Is.EqualTo(-result.max.y).Within(Tolerance));
            Assert.That(result.min.z, Is.EqualTo(-result.max.z).Within(Tolerance));
        }

        #endregion

        #region GetBlockBounds Tests

        [Test]
        public void GetBlockBounds_WhenSizesMatch_ReturnsCenteredBounds() {
            var size = new float3(16f, 1f, 48f);

            var result = ParcelGeometryUtils.GetBlockBounds(size, size);

            // When sizes match, shift is 0, so bounds are symmetric
            Assert.That(result.min.x, Is.EqualTo(-8f).Within(Tolerance));
            Assert.That(result.max.x, Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void GetBlockBounds_WhenBlockIsLarger_ShiftsBoundsCorrectly() {
            var parcelSize = new float3(16f, 1f, 24f); // 2x3 parcel
            var blockSize  = new float3(16f, 1f, 48f); // Block has fixed 6-cell depth

            var result = ParcelGeometryUtils.GetBlockBounds(parcelSize, blockSize);

            // ShiftZ = (48 - 24) / 2 = 12
            // Min.z = -48/2 - 12 = -36
            // Max.z = 48/2 - 12 = 12
            Assert.That(result.min.z, Is.EqualTo(-36f).Within(Tolerance));
            Assert.That(result.max.z, Is.EqualTo(12f).Within(Tolerance));
        }

        #endregion

        #region GetCenter Tests

        [Test]
        public void GetCenter_SymmetricBounds_ReturnsOrigin() {
            var parcelSize = new float3(16f, 1f, 24f);
            var bounds     = ParcelGeometryUtils.GetParcelBounds(parcelSize);

            var result = ParcelGeometryUtils.GetCenter(bounds);

            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(0f).Within(Tolerance));
        }

        #endregion

        #region GetParcelCenter Tests

        [Test]
        public void GetParcelCenter_ReturnsOrigin() {
            var lotSize = new int2(2, 3);

            var result = ParcelGeometryUtils.GetParcelCenter(lotSize);

            // Parcel bounds are symmetric, so center is at origin
            Assert.That(result.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.z, Is.EqualTo(0f).Within(Tolerance));
        }

        #endregion

        #region GetBlockCenter Tests

        [Test]
        public void GetBlockCenter_WithShallowParcel_ReturnsOffsetCenter() {
            // 2x3 parcel (depth = 24), block depth is always 48
            var lotSize = new int2(2, 3);

            var result = ParcelGeometryUtils.GetBlockCenter(lotSize);

            // Block is shifted to align front edge with parcel front
            // ShiftZ = (48 - 24) / 2 = 12, center.z = (-36 + 12) / 2 = -12
            Assert.That(result.z, Is.EqualTo(-12f).Within(Tolerance));
        }

        [Test]
        public void GetBlockCenter_WithDeepParcel_ReturnsOffsetCenter() {
            // 2x6 parcel (depth = 48), block depth is also 48
            var lotSize = new int2(2, 6);

            var result = ParcelGeometryUtils.GetBlockCenter(lotSize);

            // When parcel depth matches block depth, center.z = 0
            Assert.That(result.z, Is.EqualTo(0f).Within(Tolerance));
        }

        #endregion

        #region GetWorldCorners Tests

        [Test]
        public void GetWorldCorners_AtOriginNoRotation_ReturnsLocalCorners() {
            var rotation   = quaternion.identity;
            var position   = float3.zero;
            var lotSize    = new int2(2, 3); // 16x24 parcel
            var parcelSize = ParcelGeometryUtils.GetParcelSize(lotSize);

            var result = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);

            // RightFront: (-0.5, 0, 0.5) * (16, 1, 24) = (-8, 0, 12)
            Assert.That(result.a.x, Is.EqualTo(-8f).Within(Tolerance));
            Assert.That(result.a.y, Is.EqualTo(12f).Within(Tolerance));

            // LeftFront: (0.5, 0, 0.5) * (16, 1, 24) = (8, 0, 12)
            Assert.That(result.b.x, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(result.b.y, Is.EqualTo(12f).Within(Tolerance));

            // LeftBack: (0.5, 0, -0.5) * (16, 1, 24) = (8, 0, -12)
            Assert.That(result.c.x, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(result.c.y, Is.EqualTo(-12f).Within(Tolerance));

            // RightBack: (-0.5, 0, -0.5) * (16, 1, 24) = (-8, 0, -12)
            Assert.That(result.d.x, Is.EqualTo(-8f).Within(Tolerance));
            Assert.That(result.d.y, Is.EqualTo(-12f).Within(Tolerance));
        }

        [Test]
        public void GetWorldCorners_WithTranslation_OffsetsAllCorners() {
            var rotation = quaternion.identity;
            var position = new float3(100f, 0f, 200f);
            var lotSize  = new int2(2, 2); // 16x16 parcel

            var result = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);

            // All corners should be offset by (100, 200) in xz
            Assert.That(result.a.x, Is.EqualTo(100f - 8f).Within(Tolerance));
            Assert.That(result.a.y, Is.EqualTo(200f + 8f).Within(Tolerance));
            Assert.That(result.b.x, Is.EqualTo(100f + 8f).Within(Tolerance));
            Assert.That(result.b.y, Is.EqualTo(200f + 8f).Within(Tolerance));
        }

        [Test]
        public void GetWorldCorners_With90DegreeRotation_RotatesCorners() {
            var rotation = quaternion.RotateY(math.PI / 2f); // 90 degrees around Y
            var position = float3.zero;
            var lotSize  = new int2(2, 2); // 16x16 square parcel

            var result = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);

            // After 90 degree rotation, X -> -Z, Z -> X
            // Original RightFront (-8, 12) -> (12, 8)
            Assert.That(result.a.x, Is.EqualTo(8f).Within(Tolerance));
            Assert.That(result.a.y, Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void GetWorldCorners_FormQuadrilateral_WithCorrectWinding() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var lotSize  = new int2(2, 3);

            var result = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);

            // Front corners should have higher z (front = +Z)
            Assert.That(result.a.y, Is.GreaterThan(result.d.y)); // RightFront.z > RightBack.z
            Assert.That(result.b.y, Is.GreaterThan(result.c.y)); // LeftFront.z > LeftBack.z

            // Left corners should have higher x
            Assert.That(result.b.x, Is.GreaterThan(result.a.x)); // LeftFront.x > RightFront.x
            Assert.That(result.c.x, Is.GreaterThan(result.d.x)); // LeftBack.x > RightBack.x
        }

        [Test]
        public void GetWorldCorners_MatrixOverload_MatchesQuaternionOverload() {
            var rotation = quaternion.RotateY(0.5f);
            var position = new float3(50f, 0f, 75f);
            var lotSize  = new int2(3, 4);

            var resultFromQuaternion = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);

            var trs              = ParcelGeometryUtils.GetTransformMatrix(rotation, position);
            var parcelSize       = ParcelGeometryUtils.GetParcelSize(lotSize);
            var resultFromMatrix = ParcelGeometryUtils.GetWorldCorners(trs, parcelSize);

            Assert.That(resultFromMatrix.a.x, Is.EqualTo(resultFromQuaternion.a.x).Within(Tolerance));
            Assert.That(resultFromMatrix.a.y, Is.EqualTo(resultFromQuaternion.a.y).Within(Tolerance));
            Assert.That(resultFromMatrix.b.x, Is.EqualTo(resultFromQuaternion.b.x).Within(Tolerance));
            Assert.That(resultFromMatrix.b.y, Is.EqualTo(resultFromQuaternion.b.y).Within(Tolerance));
            Assert.That(resultFromMatrix.c.x, Is.EqualTo(resultFromQuaternion.c.x).Within(Tolerance));
            Assert.That(resultFromMatrix.c.y, Is.EqualTo(resultFromQuaternion.c.y).Within(Tolerance));
            Assert.That(resultFromMatrix.d.x, Is.EqualTo(resultFromQuaternion.d.x).Within(Tolerance));
            Assert.That(resultFromMatrix.d.y, Is.EqualTo(resultFromQuaternion.d.y).Within(Tolerance));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void GetParcelSize_UsedWithGetParcelBounds_ProducesCorrectSize() {
            var lotSize    = new int2(4, 5);
            var parcelSize = ParcelGeometryUtils.GetParcelSize(lotSize);
            var bounds     = ParcelGeometryUtils.GetParcelBounds(parcelSize);

            var boundsWidth = bounds.max.x - bounds.min.x;
            var boundsDepth = bounds.max.z - bounds.min.z;

            Assert.That(boundsWidth, Is.EqualTo(parcelSize.x).Within(Tolerance));
            Assert.That(boundsDepth, Is.EqualTo(parcelSize.z).Within(Tolerance));
        }

        [Test]
        public void GetWorldCorners_Area_MatchesParcelArea() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var lotSize  = new int2(2, 3);

            var result     = ParcelGeometryUtils.GetWorldCorners(rotation, position, lotSize);
            var parcelSize = ParcelGeometryUtils.GetParcelSize(lotSize);

            // Calculate width and depth from corners
            var width = math.distance(result.a, result.b);
            var depth = math.distance(result.b, result.c);

            Assert.That(width, Is.EqualTo(parcelSize.x).Within(Tolerance));
            Assert.That(depth, Is.EqualTo(parcelSize.z).Within(Tolerance));
        }

        #endregion
    }
}
