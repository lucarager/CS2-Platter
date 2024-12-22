// <copyright file="PlatterToolSystem.UI.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game.Tools;

    public partial class PlatterToolSystem : ObjectToolBaseSystem {
        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockWidth() {
            if (m_SelectedParcelSize.x > PlatterPrefabSystem.BlockSizes.x) {
                m_SelectedParcelSize.x -= 1;
            }

            m_Log.Debug("DecreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockWidth() {
            if (m_SelectedParcelSize.x < PlatterPrefabSystem.BlockSizes.z) {
                m_SelectedParcelSize.x += 1;
            }

            m_Log.Debug("IncreaseBlockWidth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void DecreaseBlockDepth() {
            if (m_SelectedParcelSize.y > PlatterPrefabSystem.BlockSizes.y) {
                m_SelectedParcelSize.y -= 1;
            }

            m_Log.Debug("DecreaseBlockDepth()");
            UpdateSelectedPrefab();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        public void IncreaseBlockDepth() {
            if (m_SelectedParcelSize.y < PlatterPrefabSystem.BlockSizes.w) {
                m_SelectedParcelSize.y += 1;
            }

            m_Log.Debug("IncreaseBlockDepth()");
            UpdateSelectedPrefab();
        }
    }
}
