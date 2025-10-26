// <copyright file="ParcelUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Game.Prefabs;

namespace Platter.Utils {
    using Game.Objects;
    using Platter.Components;
    using Platter.Constants;
    using Unity.Mathematics;

    public static class ParcelUtils {
        public static PrefabID CreatePrefabID(int2 size) {
            return new PrefabID("ParcelPrefab", $"Parcel {size.x}x{size.y}");
        }

        public enum ParcelNode {
            CornerLeftFront,
            CornerLeftBack,
            CornerRightFront,
            CornerRightBack,
            Front,
            Back,
        }

        public static float3 NodeMult(ParcelNode node) {
            return node switch {
                ParcelNode.CornerRightFront => new float3(0.5f, 0f, -0.5f),
                ParcelNode.CornerLeftFront => new float3(-0.5f, 0f, -0.5f),
                ParcelNode.CornerLeftBack => new float3(-0.5f, 0f, 0.5f),
                ParcelNode.CornerRightBack => new float3(0.5f, 0f, 0.5f),
                ParcelNode.Front => new float3(0f, 0f, 0.5f),
                ParcelNode.Back => new float3(0f, 0f, -0.5f),
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

        public static float4x4 GetTransformMatrix(Game.Objects.Transform transform) {
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
