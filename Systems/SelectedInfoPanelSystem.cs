// <copyright file="SelectedInfoPanelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Entities;
    using Colossal.UI.Binding;
    using Game.Tools;
    using Game.UI.InGame;
    using Platter.Components;
    using Platter.Utils;
    using Unity.Entities;

    /// <summary>
    /// Addes toggles to selected info panel for entites that can receive Anarchy mod components.
    /// </summary>
    public partial class SelectedInfoPanelSystem : InfoSectionBase {
        private PrefixedLogger m_Log;
        private ValueBinding<bool> m_AllowSpawningBinding;
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
            m_Log = new PrefixedLogger(nameof(SelectedInfoPanelSystem));
            m_Log.Debug($"OnCreate()");

            // Add section to UI System
            m_InfoUISystem.AddMiddleSection(this);

            // Systems & References
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // Bindings
            AddBinding(m_AllowSpawningBinding = new ValueBinding<bool>(PlatterMod.Id, "BINDING:ALLOW_SPAWNING_INFO_SECTION", false));
            AddBinding(new TriggerBinding(PlatterMod.Id, "EVENT:ALLOW_SPAWNING_TOGGLED", HandleAllowSpawningToggleEvent));
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            base.OnUpdate();

            visible = CheckEntityEligibility();

            if (visible && EntityManager.TryGetComponent<Parcel>(selectedEntity, out var parcel)) {
                m_AllowSpawningBinding.Update(parcel.m_AllowSpawning);
            }

            RequestUpdate();
        }

        /// <summary>
        /// Todo.
        /// </summary>
        private void HandleAllowSpawningToggleEvent() {
            m_Log.Debug($"HandleAllowSpawningToggleEvent()");
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
