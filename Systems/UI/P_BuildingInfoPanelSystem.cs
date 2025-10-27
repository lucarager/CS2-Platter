// <copyright file="P_SelectedInfoPanelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Colossal.Entities;
using Unity.Entities;

namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.Tools;
    using Game.UI.InGame;
    using Platter.Components;
    using Platter.Extensions;
    using Platter.Utils;

    /// <summary>
    /// Addes toggles to selected info panel for entites that can receive Anarchy mod components.
    /// </summary>
    public partial class P_BuildingInfoPanelSystem : ExtendedInfoSectionBase {
        private PrefixedLogger             m_Log;
        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private ValueBindingHelper<Entity> m_EntityBinding;

        /// <inheritdoc/>
        protected override string group => "Platter";

        protected override bool displayForUnderConstruction => true;

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
            m_Log = new PrefixedLogger(nameof(P_BuildingInfoPanelSystem));
            m_Log.Debug($"OnCreate()");

            // Add section to UI System
            m_InfoUISystem.AddMiddleSection(this);

            // Systems & References
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();

            // Binding
            m_EntityBinding = CreateBinding("INFOPANEL_BUILDING_PARCEL_ENTITY", Entity.Null);
            CreateTrigger<Entity>("INFOPANEL_SELECT_PARCEL_ENTITY", SelectEntity);
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            base.OnUpdate();

            if (EntityManager.TryGetComponent<LinkedParcel>(selectedEntity, out var linkedParcel)) {
                visible = true;
                m_EntityBinding.Value = linkedParcel.m_Parcel;
            } else {
                m_EntityBinding.Value = Entity.Null;
                visible               = false;
            }

            RequestUpdate();
        }

        private void SelectEntity(Entity entity) {
            m_SelectedInfoUISystem.SetSelection(entity);
        }
    }
}
