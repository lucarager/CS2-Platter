using Game.Prefabs;
using Game.UI;
using Unity.Mathematics;
using UnityEngine;

namespace Platter.Systems {
    public partial class PrefabLoadSystem : UISystemBase {

        private bool CreatePrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, NetLaneGeometryPrefab netLaneGeoPrefab, UIObject zonePrefabUIObject, StaticObjectPrefab roadArrowFwd) {
            var logMethodPrefix = $"CreatePrefab()";
            var width = m_CellSize * lotWidth;
            var depth = m_CellSize * lotDepth;
            var widthStep = width / 4f;
            var depthStep = depth / 4f;
            var baseHeight = 1f;

            // Create our new prefab
            var placeableLotPrefab = ScriptableObject.CreateInstance<ParcelPrefab>();
            placeableLotPrefab.name = $"{m_PrefabNamePrefix} {lotWidth}x{lotDepth}";
            placeableLotPrefab.m_LotWidth = lotWidth;
            placeableLotPrefab.m_LotDepth = lotDepth;

            // Adding PlaceableObject Data.
            var placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            placeableLotPrefab.AddComponentFrom(placeableObject);

            // Adding ZoneBlock data.
            placeableLotPrefab.m_ZoneBlock = roadPrefab.m_ZoneBlock;

            // Adding SubObjects.
            var subObjects = ScriptableObject.CreateInstance<ObjectSubObjects>();
            subObjects.m_SubObjects = new ObjectSubObjectInfo[] {
                new () {
                    m_Object = roadArrowFwd,
                    m_Position = new float3(0f, baseHeight, (depth / 2) + 3f),
                    m_Rotation = new quaternion(0, 1, 0, 0),
                }
            };
            placeableLotPrefab.AddComponentFrom(subObjects);

            // Create and populate the new UIObject for our cloned Prefab
            var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = zonePrefabUIObject.m_Icon;
            placeableLotPrefabUIObject.name = m_PrefabNamePrefix;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Group = zonePrefabUIObject.m_Group;
            placeableLotPrefabUIObject.active = zonePrefabUIObject.active;
            placeableLotPrefab.AddComponentFrom(placeableLotPrefabUIObject);

            // Sublanes
            //var subLanes = ScriptableObject.CreateInstance<ObjectSubLanes>();
            //subLanes.m_SubLanes = new ObjectSubLaneInfo[] {
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
            //};
            //placeableLotPrefab.AddComponentFrom(subLanes);

            // Try to add it to the prefab System
            return prefabSystem.AddPrefab(placeableLotPrefab);
        }
    }
}
