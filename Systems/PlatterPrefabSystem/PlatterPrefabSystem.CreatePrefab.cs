// <copyright file="PlatterPrefabSystem.CreateParcelPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Json;
    using Game.Prefabs;
    using Game.UI;
    using UnityEngine;

    /// <summary>
    /// todo.
    /// </summary>
    public partial class PlatterPrefabSystem : UISystemBase {
        /// <summary>
        /// todo.
        /// </summary>
        private bool CreateParcelPrefab(int lotWidth, int lotDepth, RoadPrefab roadPrefab, UIObject zonePrefabUIObject, UIAssetCategoryPrefab uiCategoryPrefab) {
            var name = $"{PrefabNamePrefix} {lotWidth}x{lotDepth}";
            var icon = $"coui://platter/{PrefabNamePrefix}_{lotWidth}x{lotDepth}.svg";

            // Point our new prefab
            var parcelPrefabBase = ScriptableObject.CreateInstance<ParcelPrefab>();
            parcelPrefabBase.name = name;
            parcelPrefabBase.m_LotWidth = lotWidth;
            parcelPrefabBase.m_LotDepth = lotDepth;

            // Adding PlaceableObject Data.
            var placeableObject = ScriptableObject.CreateInstance<PlaceableObject>();
            placeableObject.m_ConstructionCost = 0;
            placeableObject.m_XPReward = 0;
            parcelPrefabBase.AddComponentFrom(placeableObject);

            // Adding ZoneBlock data.
            parcelPrefabBase.m_ZoneBlock = roadPrefab.m_ZoneBlock;

            // Point and populate the new UIObject for our cloned Prefab
            var placeableLotPrefabUIObject = ScriptableObject.CreateInstance<UIObject>();
            placeableLotPrefabUIObject.m_Icon = icon;
            placeableLotPrefabUIObject.name = PrefabNamePrefix;
            placeableLotPrefabUIObject.m_IsDebugObject = zonePrefabUIObject.m_IsDebugObject;
            placeableLotPrefabUIObject.m_Priority = zonePrefabUIObject.m_Priority;
            placeableLotPrefabUIObject.m_Group = uiCategoryPrefab;
            placeableLotPrefabUIObject.active = zonePrefabUIObject.active;
            parcelPrefabBase.AddComponentFrom(placeableLotPrefabUIObject);

            m_Log.Debug($"Created Parcel SelectedPrefabBase with uiTag {parcelPrefabBase.uiTag}");

            // Try to add it to the prefab System
            var success = m_PrefabSystem.AddPrefab(parcelPrefabBase);

            if (success) {
                // Todo can we set data here instead of the system?
                var prefabEntity = m_PrefabSystem.GetEntity(parcelPrefabBase);
                m_PrefabBases.Add(parcelPrefabBase);
                m_PrefabEntities.Add(parcelPrefabBase, prefabEntity);
            }

            return success;
        }
    }
}
