﻿// <copyright file="P_SelectedInfoPanelSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Colossal.Entities;
using Game.Prefabs;

namespace Platter.Systems {
    using System;
    using Colossal.UI.Binding;
    using Game.Tools;
    using Game.UI.InGame;
    using Platter.Components;
    using Platter.Extensions;
    using Platter.Utils;
    using Unity.Entities;

    /// <summary>
    /// Addes toggles to selected info panel for entites that can receive Anarchy mod components.
    /// </summary>
    public partial class P_ParcelInfoPanelSystem : ExtendedInfoSectionBase {
        private PrefixedLogger             m_Log;
        private ToolSystem                 m_ToolSystem;
        private P_ZoneCacheSystem          m_ZoneCacheSystem;
        private ObjectToolSystem           m_ObjectToolSystem;
        private ValueBindingHelper<UIData> m_DataBinding;
        private ValueBindingHelper<Entity> m_DataBuildingBinding;
        private ValueBindingHelper<Entity> m_DataRoadBinding;

        public readonly struct UIData : IJsonWritable {
            public readonly string Name;
            public readonly string Zoning;

            public UIData(string name,
                          string zoning) {
                Name   = name;
                Zoning = zoning;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("name");
                writer.Write(Name);

                writer.PropertyName("zoning");
                writer.Write(Zoning);

                writer.TypeEnd();
            }
        }

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
            m_ToolSystem       = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_ZoneCacheSystem  = World.GetOrCreateSystemManaged<P_ZoneCacheSystem>();

            // Bindings
            m_DataBinding         = CreateBinding<UIData>("INFOPANEL_PARCEL_DATA", new UIData());
            m_DataBuildingBinding = CreateBinding<Entity>("INFOPANEL_PARCEL_DATA_BUILDING", Entity.Null);
            m_DataRoadBinding     = CreateBinding<Entity>("INFOPANEL_PARCEL_DATA_ROAD", Entity.Null);
            CreateTrigger("INFOPANEL_PARCEL_RELOCATE", new Action<Entity>(OnRelocate));
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            base.OnUpdate();

            if (EntityManager.TryGetComponent<Parcel>(selectedEntity, out var parcel)) {
                visible = true;
                var hasZone   = m_ZoneCacheSystem.ZoneUIData.TryGetValue(parcel.m_PreZoneType.m_Index, out var zoneData);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(selectedEntity);

                m_DataBinding.Value = new UIData(
                    name: prefabRef.ToString(),
                    zoning: hasZone ? zoneData.Name : ""
                );
                m_DataBuildingBinding.Value = parcel.m_Building;
                m_DataRoadBinding.Value     = parcel.m_RoadEdge;
            } else {
                visible                     = false;
                m_DataBinding.Value         = new UIData();
                m_DataBuildingBinding.Value = Entity.Null;
                m_DataRoadBinding.Value     = Entity.Null;
            }

            RequestUpdate();
        }

        /// <summary>
        /// Returns whether the component is eligible for Platter infoview additions.
        /// </summary>
        private bool CheckEntityEligibility() {
            var isParcelCOmponent = EntityManager.HasComponent<Parcel>(selectedEntity);
            return isParcelCOmponent;
        }

        private void OnRelocate(Entity entity) {
            m_ObjectToolSystem.StartMoving(entity);
            m_ToolSystem.activeTool = m_ObjectToolSystem;
        }
    }
}
