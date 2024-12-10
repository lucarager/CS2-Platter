// <copyright file="PrefabLoadSystem.CreatePrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Prefabs;
    using Game.UI;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class PrefabLoadSystem : UISystemBase {
        /// <summary>
        /// todo.
        /// </summary>
        private bool CreatePrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, NetLaneGeometryPrefab netLaneGeoPrefab, UIObject zonePrefabUIObject, StaticObjectPrefab roadArrowFwd) {
            var width = CellSize * lotWidth;
            var depth = CellSize * lotDepth;
            var name = $"{PrefabNamePrefix} {lotWidth}x{lotDepth}";
            var icon = $"coui://platter/{PrefabNamePrefix}_{lotWidth}x{lotDepth}.svg";
            var baseHeight = 1f;

            // Create our new prefab
            ParcelPrefab parcelPrefabBase = ScriptableObject.CreateInstance<ParcelPrefab>();
            parcelPrefabBase.name = name;
            parcelPrefabBase.m_LotWidth = lotWidth;
            parcelPrefabBase.m_LotDepth = lotDepth;

            // Adding PlaceableObject Data.
            PlaceableObject placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            parcelPrefabBase.AddComponentFrom(placeableObject);

            // Adding ZoneBlock data.
            parcelPrefabBase.m_ZoneBlock = roadPrefab.m_ZoneBlock;

            // Adding SubObjects.
            ObjectSubObjects subObjects = ScriptableObject.CreateInstance<ObjectSubObjects>();
            subObjects.m_SubObjects = new ObjectSubObjectInfo[] {
                new () {
                    m_Object = roadArrowFwd,
                    m_Position = new float3(0f, baseHeight, (depth / 2) + 3f),
                    m_Rotation = new quaternion(0, 1, 0, 0),
                }
            };

            // parcelPrefabBase.AddComponentFrom(subObjects);

            // Create and populate the new UIObject for our cloned Prefab
            UIObject placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = icon;
            placeableLotPrefabUIObject.name = PrefabNamePrefix;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Group = zonePrefabUIObject.m_Group;
            placeableLotPrefabUIObject.active = zonePrefabUIObject.active;
            parcelPrefabBase.AddComponentFrom(placeableLotPrefabUIObject);

            // Sublanes
            // var subLanes = ScriptableObject.CreateInstance<ObjectSubLanes>();
            // var widthStep = width / 4f;
            // var depthStep = depth / 4f;
            // subLanes.m_SubLanes = new ObjectSubLaneInfo[] {
            //    new () {
            //        m_LanePrefab = netLaneGeoPrefab,
            //        m_BezierCurve = new Bezier4x3(
            //            new float3(-width / 2, baseHeight, -depthStep * 2),
            //            new float3(-width / 2, baseHeight, -depthStep),
            //            new float3(-width / 2, baseHeight, depthStep),
            //            new float3(-width / 2, baseHeight, depthStep * 2)
            //        ),
            //        m_NodeIndex = new int2(0, 1)
            //    },
            //    new () {
            //        m_LanePrefab = netLaneGeoPrefab,
            //        m_BezierCurve = new Bezier4x3(
            //            new float3(width / 2, baseHeight, -depthStep * 2),
            //            new float3(width / 2, baseHeight, -depthStep),
            //            new float3(width / 2, baseHeight, depthStep),
            //            new float3(width / 2, baseHeight, depthStep * 2)
            //        ),
            //        m_NodeIndex = new int2(2, 3)
            //    },
            //    new () {
            //        m_LanePrefab = netLaneGeoPrefab,
            //        m_BezierCurve = new Bezier4x3(
            //            new float3(-widthStep * 2, baseHeight, -depth / 2),
            //            new float3(-widthStep, baseHeight, -depth / 2),
            //            new float3(widthStep, baseHeight, -depth / 2),
            //            new float3(widthStep * 2, baseHeight, -depth / 2)
            //        ),
            //        m_NodeIndex = new int2(4, 5)
            //    },
            //    new () {
            //        m_LanePrefab = netLaneGeoPrefab,
            //        m_BezierCurve = new Bezier4x3(
            //            new float3(-widthStep * 2, baseHeight, depth / 2),
            //            new float3(-widthStep, baseHeight, depth / 2),
            //            new float3(widthStep, baseHeight, depth / 2),
            //            new float3(widthStep * 2, baseHeight, depth / 2)
            //        ),
            //        m_NodeIndex = new int2(6, 7)
            //    },
            // };
            // parcelPrefabBase.AddComponentFrom(subLanes);
            m_Log.Debug($"Created Parcel PrefabBase with uiTag {parcelPrefabBase.uiTag}");

            // Try to add it to the prefab System
            return m_PrefabSystem.AddPrefab(parcelPrefabBase);
        }
    }
}
