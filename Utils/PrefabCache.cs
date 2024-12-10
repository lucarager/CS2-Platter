// <copyright file="PrefabCache.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    using System.Collections.Generic;
    using Game.Prefabs;

    internal class PrefabCache {
        private static Dictionary<string, PrefabID> m_PrefabCache = new();

        public static void Add(string name, PrefabID prefabID) {
            m_PrefabCache[name] = prefabID;
        }
    }
}
