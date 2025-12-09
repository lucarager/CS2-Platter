// <copyright file="ParcelUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using Components;
    using Constants;
    using Game.Prefabs;
    using Unity.Mathematics;
    using UnityEngine;
    using Transform = Game.Objects.Transform;

    #endregion

    public static class ParcelUtils {
        public enum ParcelNode {
            CornerLeftFront,
            CornerLeftBack,
            CornerRightFront,
            CornerRightBack,
            FrontAccess,
            RightAccess,
            LeftAccess,
            BackAccess,
        }

        public static PrefabID GetPrefabID(int width, int depth, bool placeholder = false) {
            var category = placeholder ? "ParcelPlaceholderPrefab" : "ParcelPrefab";
            var name     = $"Parcel {width}x{depth}";
            return new PrefabID(category, name);
        }

        public static PrefabID GetPrefabID(int2 size, bool placeholder = false) { return GetPrefabID(size.x, size.y, placeholder); }

        public static int GetCustomHashCode(PrefabID prefabID, bool placeholder = false) {
            return Hash128.Compute(prefabID.GetType() + prefabID.GetName() + placeholder.ToString()).GetHashCode();
        }

        public static float3 NodeMult(ParcelNode node) {
            const float leftX  = 0.5f;
            const float rightX = -0.5f;
            const float frontZ = 0.5f;
            const float backZ  = -0.5f;

            return node switch {
                ParcelNode.CornerLeftFront => new float3(leftX, 0f, frontZ),
                ParcelNode.CornerRightFront => new float3(rightX, 0f, frontZ),
                ParcelNode.CornerLeftBack => new float3(leftX, 0f, backZ),
                ParcelNode.CornerRightBack => new float3(rightX, 0f, backZ),
                ParcelNode.FrontAccess => new float3(0f, 0f, frontZ),
                ParcelNode.LeftAccess => new float3(leftX, 0f, 0f),
                ParcelNode.RightAccess => new float3(rightX, 0f, 0f),
                ParcelNode.BackAccess => new float3(0f, 0f, backZ),
                _ => new float3(0f, 0f, 0f),
            };
        }

        public static float3 GetParcelSize(ParcelData parcelData) {
            return new float3(
                parcelData.m_LotSize.x * DimensionConstants.CellSize,
                DimensionConstants.ParcelHeight,
                parcelData.m_LotSize.y * DimensionConstants.CellSize
            );
        }

        public static float4x4 GetTransformMatrix(Transform transform) {
            return new float4x4(transform.m_Rotation, transform.m_Position);
        }

        public static float3 GetWorldPosition(float4x4 trs, float3 center, float3 position) {
            return math.transform(trs, center + position);
        }

        public static float3 GetWorldPosition(Transform transform, float3 center) {
            var trs = GetTransformMatrix(transform);
            return math.transform(trs, center);
        }

        public static float3 GetWorldPosition(Transform transform, float3 center, float3 position) {
            var trs = GetTransformMatrix(transform);
            return math.transform(trs, center + position);
        }
    }
}