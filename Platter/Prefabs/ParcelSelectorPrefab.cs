// <copyright file="ParcelSelectorPrefab.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Prefabs {
    /// <summary>
    /// Pure-UI prefab shown in the vanilla toolbar. Selecting it routes
    /// (via Harmony patches) to the sized <see cref="ParcelPlaceholderPrefab"/>
    /// that matches the user's currently chosen width/depth.
    /// </summary>
    public class ParcelSelectorPrefab : ParcelPlaceholderPrefab {
        public ParcelSelectorPrefab() { }
    }
}
