// <copyright file="ParcelUtilsTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Tests.Utils {
    using Platter.Constants;
    using Platter.Utils;
    using Unity.Mathematics;

    /// <summary>
    /// Unit tests for <see cref="ParcelUtils"/> static utility methods.
    /// These tests verify pure math and logic without requiring Unity runtime.
    /// </summary>
    /// <remarks>
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

        [Test]
        public void GetPrefabID_PlaceholderAndNonPlaceholder_ReturnSameName() {
            var prefabIdNormal      = ParcelUtils.GetPrefabID(2, 2, placeholder: false);
            var prefabIdPlaceholder = ParcelUtils.GetPrefabID(2, 2, placeholder: true);

            // Both have the same name, but different categories (tested via hash in-game)
            Assert.That(prefabIdNormal.GetName(), Is.EqualTo(prefabIdPlaceholder.GetName()));
        }

        #endregion

        #region NodeMult Tests

        [Test]
        public void NodeMult_CornerLeftFront_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftFront);

            Assert.That(result.x, Is.EqualTo(0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0.5f));
        }

        [Test]
        public void NodeMult_CornerRightFront_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightFront);

            Assert.That(result.x, Is.EqualTo(-0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0.5f));
        }

        [Test]
        public void NodeMult_CornerLeftBack_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftBack);

            Assert.That(result.x, Is.EqualTo(0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(-0.5f));
        }

        [Test]
        public void NodeMult_CornerRightBack_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightBack);

            Assert.That(result.x, Is.EqualTo(-0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(-0.5f));
        }

        [Test]
        public void NodeMult_FrontAccess_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.FrontAccess);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0.5f));
        }

        [Test]
        public void NodeMult_BackAccess_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.BackAccess);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(-0.5f));
        }

        [Test]
        public void NodeMult_LeftAccess_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.LeftAccess);

            Assert.That(result.x, Is.EqualTo(0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        [Test]
        public void NodeMult_RightAccess_ReturnsCorrectMultiplier() {
            var result = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.RightAccess);

            Assert.That(result.x, Is.EqualTo(-0.5f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        [Test]
        public void NodeMult_InvalidNode_ReturnsZero() {
            var result = ParcelUtils.NodeMult((ParcelUtils.ParcelNode)999);

            Assert.That(result.x, Is.EqualTo(0f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f));
        }

        #endregion

        #region GetTransformMatrix Tests

        [Test]
        public void GetTransformMatrix_IdentityRotationAtOrigin_ReturnsIdentityMatrix() {
            var rotation = quaternion.identity;
            var position = float3.zero;

            var result = ParcelUtils.GetTransformMatrix(rotation, position);

            // Check that the translation part is at origin
            Assert.That(result.c3.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.c3.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.c3.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void GetTransformMatrix_WithPosition_ReturnsCorrectTranslation() {
            var rotation = quaternion.identity;
            var position = new float3(10f, 20f, 30f);

            var result = ParcelUtils.GetTransformMatrix(rotation, position);

            Assert.That(result.c3.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(result.c3.y, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(result.c3.z, Is.EqualTo(30f).Within(0.0001f));
        }

        #endregion

        #region GetWorldPosition Tests

        [Test]
        public void GetWorldPosition_AtOriginWithNoOffset_ReturnsCenter() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var trs      = ParcelUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(5f, 0f, 5f);

            var result = ParcelUtils.GetWorldPosition(trs, center, float3.zero);

            Assert.That(result.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void GetWorldPosition_WithOffset_AddsOffsetToCenter() {
            var rotation = quaternion.identity;
            var position = float3.zero;
            var trs      = ParcelUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(0f, 0f, 0f);
            var offset   = new float3(1f, 2f, 3f);

            var result = ParcelUtils.GetWorldPosition(trs, center, offset);

            Assert.That(result.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void GetWorldPosition_WithTranslation_TranslatesResult() {
            var rotation = quaternion.identity;
            var position = new float3(100f, 0f, 100f);
            var trs      = ParcelUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(5f, 0f, 5f);

            var result = ParcelUtils.GetWorldPosition(trs, center, float3.zero);

            Assert.That(result.x, Is.EqualTo(105f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(105f).Within(0.0001f));
        }

        [Test]
        public void GetWorldPosition_With90DegreeRotation_RotatesCorrectly() {
            // 90 degrees around Y axis
            var rotation = quaternion.RotateY(math.PI / 2f);
            var position = float3.zero;
            var trs      = ParcelUtils.GetTransformMatrix(rotation, position);
            var center   = new float3(10f, 0f, 0f); // 10 units in X direction

            var result = ParcelUtils.GetWorldPosition(trs, center, float3.zero);

            // After 90 degree rotation around Y, X becomes -Z
            Assert.That(result.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(-10f).Within(0.0001f));
        }

        #endregion

        #region Integration: NodeMult with Parcel Size

        [Test]
        public void NodeMult_MultipliedByParcelSize_ReturnsCorrectCornerPosition() {
            // A 2x3 parcel has size: 2*8=16 width, 3*8=24 depth
            var parcelSize = new float3(16f, DimensionConstants.ParcelHeight, 24f);
            var nodeMult   = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftFront);

            var cornerPosition = nodeMult * parcelSize;

            // CornerLeftFront is at (0.5, 0, 0.5) * size = (8, 0, 12)
            Assert.That(cornerPosition.x, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(cornerPosition.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(cornerPosition.z, Is.EqualTo(12f).Within(0.0001f));
        }

        [Test]
        public void NodeMult_AllCorners_FormRectangle() {
            var parcelSize = new float3(16f, 1f, 24f); // 2x3 lot

            var leftFront  = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftFront) * parcelSize;
            var rightFront = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightFront) * parcelSize;
            var leftBack   = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerLeftBack) * parcelSize;
            var rightBack  = ParcelUtils.NodeMult(ParcelUtils.ParcelNode.CornerRightBack) * parcelSize;

            // Width should be consistent (leftFront.x - rightFront.x = 16)
            Assert.That(leftFront.x - rightFront.x, Is.EqualTo(16f).Within(0.0001f));
            Assert.That(leftBack.x - rightBack.x, Is.EqualTo(16f).Within(0.0001f));

            // Depth should be consistent (leftFront.z - leftBack.z = 24)
            Assert.That(leftFront.z - leftBack.z, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(rightFront.z - rightBack.z, Is.EqualTo(24f).Within(0.0001f));
        }

        #endregion
    }
}
