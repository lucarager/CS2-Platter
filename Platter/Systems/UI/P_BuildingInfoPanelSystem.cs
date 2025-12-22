// <copyright file="P_BuildingInfoPanelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    #region Using Statements

    using Colossal.Entities;
    using Colossal.UI.Binding;
    using Components;
    using Extensions;
    using Game.UI.InGame;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// Addes toggles to selected info panel for entites that can receive Anarchy mod components.
    /// </summary>
    public partial class P_BuildingInfoPanelSystem : ExtendedInfoSectionBase {
        private PrefixedLogger             m_Log;
        private SelectedInfoUISystem       m_SelectedInfoUISystem;
        private ValueBindingHelper<Entity> m_EntityBinding;

        protected override bool displayForUnderConstruction => true;

        /// <inheritdoc/>
        protected override string group => "Platter";

        /// <inheritdoc/>
        public override void OnWriteProperties(IJsonWriter writer) { }

        /// <inheritdoc/>
        protected override void OnProcess() { }

        /// <inheritdoc/>
        protected override void Reset() { }

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(P_BuildingInfoPanelSystem));
            m_Log.Debug("OnCreate()");

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
                visible               = true;
                m_EntityBinding.Value = linkedParcel.m_Parcel;
            } else {
                m_EntityBinding.Value = Entity.Null;
                visible               = false;
            }

            RequestUpdate();
        }

        private void SelectEntity(Entity entity) { m_SelectedInfoUISystem.SetSelection(entity); }
    }
}