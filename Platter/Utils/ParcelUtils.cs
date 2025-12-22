// <copyright file="ParcelUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using Components;
    using Constants;
    using Game.Prefabs;
    using Game.Zones;
    using Systems;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
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
            return UnityEngine.Hash128.Compute(prefabID.GetType() + prefabID.GetName() + placeholder.ToString()).GetHashCode();
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
            return GetTransformMatrix(transform.m_Rotation, transform.m_Position);
        }

        public static float4x4 GetTransformMatrix(quaternion rotation, float3 position) {
            return new float4x4(rotation, position);
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

        /// <summary>
        /// Classifies the zoning state of a parcel by analyzing its cell zones within the parcel bounds.
        /// Updates the parcel's PreZoneType and ZoningUniform state flag.
        /// </summary>
        /// <param name="parcel">The parcel to update.</param>
        /// <param name="block">The block containing cell information.</param>
        /// <param name="parcelData">The parcel data containing lot size information.</param>
        /// <param name="cellBuffer">The buffer containing cell data.</param>
        [BurstCompile]
        public static void ClassifyParcelZoning(ref Parcel parcel, in Block block, in ParcelData parcelData, in DynamicBuffer<Cell> cellBuffer) {
            var isZoningUniform = true;
            var cachedZone = P_ZoneCacheSystem.UnzonedZoneType;

            for (var col = 0; col < block.m_Size.x; col++) {
                for (var row = 0; row < block.m_Size.y; row++) {
                    var index = row * block.m_Size.x + col;
                    var cell = cellBuffer[index];

                    if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                        continue;
                    }

                    if (cachedZone.m_Index == P_ZoneCacheSystem.UnzonedZoneType.m_Index) {
                        cachedZone = cell.m_Zone;
                    } else if (cell.m_Zone.m_Index != cachedZone.m_Index) {
                        isZoningUniform = false;
                    }
                }
            }

            parcel.m_PreZoneType = cachedZone;
            if (isZoningUniform) {
                parcel.m_State |= ParcelState.ZoningUniform;
            } else {
                parcel.m_State &= ~ParcelState.ZoningUniform;
            }
        }
    }
}