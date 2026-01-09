// <copyright file="ParcelUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using System;
    using Components;
    using Game.Prefabs;
    using Game.Zones;
    using Systems;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Mathematics;
    using static Game.UI.NameSystem;

    #endregion

    /// <summary>
    /// Utility class for parcel-related operations (prefab IDs, hashing, zoning).
    /// For geometry calculations, use <see cref="ParcelGeometryUtils"/>.
    /// </summary>
    [BurstCompile]
    public static class ParcelUtils {
        /// <summary>
        /// Defines the node positions on a parcel (corners and edge access points).
        /// </summary>
        /// <remarks>
        /// Deprecated: Use <see cref="ParcelGeometryUtils.ParcelNode"/> instead.
        /// This alias is kept for backward compatibility.
        /// </remarks>
        [Obsolete("Use ParcelGeometryUtils.ParcelNode instead.")]
        public enum ParcelNode {
            CornerLeftFront  = ParcelGeometryUtils.ParcelNode.CornerLeftFront,
            CornerLeftBack   = ParcelGeometryUtils.ParcelNode.CornerLeftBack,
            CornerRightFront = ParcelGeometryUtils.ParcelNode.CornerRightFront,
            CornerRightBack  = ParcelGeometryUtils.ParcelNode.CornerRightBack,
            FrontAccess      = ParcelGeometryUtils.ParcelNode.FrontAccess,
            RightAccess      = ParcelGeometryUtils.ParcelNode.RightAccess,
            LeftAccess       = ParcelGeometryUtils.ParcelNode.LeftAccess,
            BackAccess       = ParcelGeometryUtils.ParcelNode.BackAccess,
        }

        public static PrefabID GetPrefabID(int width, int depth, bool placeholder = false) {
            var prefix = placeholder ? "ParcelPlaceholder" : "Parcel";
            var type   = placeholder ? "ParcelPlaceholderPrefab" : "ParcelPrefab";
            var name   = $"{prefix} {width}x{depth}";
            return new PrefabID(type, name);
        }

        public static PrefabID GetPrefabID(int2 size, bool placeholder = false) {
            return GetPrefabID(size.x, size.y, placeholder);
        }

        public static int GetHashCode(int2 size, bool placeholder = false) {
            return placeholder ? 
                UnityEngine.Hash128.Compute($"ParcelPlaceholder {size.x}x{size.y}").GetHashCode() : 
                UnityEngine.Hash128.Compute($"Parcel {size.x}x{size.y}").GetHashCode();
        }

        public static int GetHashCode(PrefabID prefabID) {
            return UnityEngine.Hash128.Compute($"{prefabID.GetName()}").GetHashCode();
        }

        /// <summary>
        /// Returns the multiplier vector for a parcel node position.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.NodeMult instead.")]
        public static float3 NodeMult(ParcelNode node) {
            return ParcelGeometryUtils.NodeMult((ParcelGeometryUtils.ParcelNode)node);
        }

        /// <summary>
        /// Calculates the parcel size from parcel data.
        /// </summary>
        /// <remarks>
        /// Consider using <see cref="ParcelGeometryUtils.GetParcelSize(int2)"/> for lot size input.
        /// </remarks>
        [Obsolete("Use ParcelGeometryUtils.GetParcelSize instead.")]
        public static float3 GetParcelSize(ParcelData parcelData) {
            return ParcelGeometryUtils.GetParcelSize(parcelData.m_LotSize);
        }

        /// <summary>
        /// Builds a transform matrix from a transform.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.GetTransformMatrix instead.")]
        public static float4x4 GetTransformMatrix(Game.Objects.Transform transform) {
            return ParcelGeometryUtils.GetTransformMatrix(transform);
        }

        /// <summary>
        /// Builds a transform matrix from rotation and position.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.GetTransformMatrix instead.")]
        public static float4x4 GetTransformMatrix(quaternion rotation, float3 position) {
            return ParcelGeometryUtils.GetTransformMatrix(rotation, position);
        }

        /// <summary>
        /// Transforms a local position to world space.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.GetWorldPosition instead.")]
        public static float3 GetWorldPosition(float4x4 trs, float3 center, float3 position) {
            return ParcelGeometryUtils.GetWorldPosition(trs, center, position);
        }

        /// <summary>
        /// Transforms a local position to world space.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.GetWorldPosition instead.")]
        public static float3 GetWorldPosition(Game.Objects.Transform transform, float3 center) {
            return ParcelGeometryUtils.GetWorldPosition(transform, center);
        }

        /// <summary>
        /// Transforms a local position to world space.
        /// </summary>
        [Obsolete("Use ParcelGeometryUtils.GetWorldPosition instead.")]
        public static float3 GetWorldPosition(Game.Objects.Transform transform, float3 center, float3 position) {
            return ParcelGeometryUtils.GetWorldPosition(transform, center, position);
        }

        /// <summary>
        /// Classifies the zoning state of a parcel by analyzing its cell zones within the parcel bounds.
        /// Updates the parcel's PreZoneType and ZoningUniform state flag.
        /// </summary>
        /// <param name="parcel">The parcel to update.</param>
        /// <param name="block">The block containing cell information.</param>
        /// <param name="parcelData">The parcel data containing lot size information.</param>
        /// <param name="cellBuffer">The buffer containing cell data.</param>
        /// <param name="unzonedZoneType">The zone type representing unzoned cells (passed to avoid static field access in Burst).</param>
#if USE_BURST
        [BurstCompile]
#endif
        public static void ClassifyParcelZoning(ref Parcel parcel, in Block block, in ParcelData parcelData, in DynamicBuffer<Cell> cellBuffer, ZoneType unzonedZoneType) {
            var isZoningUniform = true;
            var cachedZone = unzonedZoneType;

            for (var col = 0; col < block.m_Size.x; col++) {
                for (var row = 0; row < block.m_Size.y; row++) {
                    var index = row * block.m_Size.x + col;
                    var cell = cellBuffer[index];

                    if (col >= parcelData.m_LotSize.x || row >= parcelData.m_LotSize.y) {
                        continue;
                    }

                    // Catch any edge cases where the zoning is not yet set to custom unzoned
                    if (cell.m_Zone.Equals(ZoneType.None)) {
                        cell.m_Zone = unzonedZoneType;
                    }

                    if (cachedZone.m_Index == unzonedZoneType.m_Index) {
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