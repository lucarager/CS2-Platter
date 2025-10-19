// <copyright file="P_SelectedInfoPanelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.Tools;
    using Game.UI.InGame;
    using Platter.Components;
    using Platter.Utils;

    /// <summary>
    /// Addes toggles to selected info panel for entites that can receive Anarchy mod components.
    /// </summary>
    public partial class P_ParcelInfoPanelSystem : InfoSectionBase {
        private PrefixedLogger m_Log;
        private ToolSystem m_ToolSystem;

        /// <inheritdoc/>
        protected override string group => "Platter";

        /// <inheritdoc/>
        public override void OnWriteProperties(IJsonWriter writer) {
        }

        /// <inheritdoc/>
        protected override void OnProcess() {
        }

        /// <inheritdoc/>
        protected override void Reset() {
        }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_ParcelInfoPanelSystem));
            m_Log.Debug($"OnCreate()");

            // Add section to UI System
            m_InfoUISystem.AddMiddleSection(this);

            // Systems & References
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            base.OnUpdate();

            visible = CheckEntityEligibility();

            RequestUpdate();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        /// <returns>Whether the component is eligible for Platter infoview additions.</returns>
        private bool CheckEntityEligibility() {
            var isParcelCOmponent = EntityManager.HasComponent<Parcel>(selectedEntity);
            return isParcelCOmponent;
        }
    }
}
