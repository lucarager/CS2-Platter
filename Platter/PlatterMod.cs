// <copyright file="PlatterMod.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using Colossal;
    using Colossal.Localization;
    using Colossal.Logging;
    using Components;
    using Game;
    using Game.Input;
    using Game.Modding;
    using Game.Prefabs;
    using Game.Serialization;
    using Game.Simulation;
    using Game.Tools;
    using L10n;
    using LucaModsCommon.Extensions;
    using LucaModsCommon.Mod;
    using Newtonsoft.Json;
    using Settings;
    using Systems;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Mod entry point.
    /// </summary>
    public class PlatterMod : LucaModBase<PlatterMod> {
        /// <inheritdoc/>
        public override string ModName => "Platter";

        /// <inheritdoc/>
        public override string Id => "Platter";

        /// <inheritdoc/>
        protected override string UiHostPrefix => "platter";

        /// <summary>
        /// Gets or sets a value indicating whether the mod is in test mode.
        /// Re-exposed from base class for cross-assembly access.
        /// </summary>
        internal new bool IsTestMode {
            get => base.IsTestMode;
            set => base.IsTestMode = value;
        }

        /// <summary>
        /// Gets the mod's typed settings (shadows base.Settings).
        /// </summary>
        public new PlatterModSettings Settings => (PlatterModSettings)base.Settings;

        /// <inheritdoc/>
        protected override ModSetting CreateSettings(IMod mod) => new PlatterModSettings(mod);

        /// <inheritdoc/>
        protected override IDictionarySource CreateEnUsLocalization(ModSetting settings)
            => new EnUsConfig((PlatterModSettings)settings);

        /// <inheritdoc/>
        protected override void RegisterSystems(UpdateSystem updateSystem) {
            ModLog.Debug("RegisterSystems()");

            // (De)Serializaztion
            updateSystem.UpdateBefore<PreDeserialize<P_ParcelSearchSystem>>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ParcelSubBlockDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_ConnectedParcelDeserializeSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_LoadInitConnectedParcelSystem>(SystemUpdatePhase.Deserialize);
            updateSystem.UpdateAfter<P_LoadZoneResolverSystem, ResolvePrefabsSystem>(SystemUpdatePhase.Deserialize);

            // Prefabs
            updateSystem.UpdateAfter<P_PrefabsCreateSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ParcelInitializeSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_UnzonedInitializeSystem, ZoneSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_ZoneCacheSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAfter<P_BuildingCacheSystem>(SystemUpdatePhase.PrefabUpdate);

            // Buildings
            updateSystem.UpdateAfter<P_BuildingInitializeSystem, BuildingConstructionSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<P_BuildingTransformCheckSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_BuildingToParcelReferenceSystem>(SystemUpdatePhase.Modification2);

            // Roads
            updateSystem.UpdateAt<P_ConnectedParcelSystem>(SystemUpdatePhase.Modification1);

            // Parcels
            updateSystem.UpdateAt<P_PlaceholderSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_PlaceholderPatchSystem>(SystemUpdatePhase.Modification1);
            updateSystem.UpdateAt<P_ParcelUpdateSystem>(SystemUpdatePhase.Modification2);
            updateSystem.UpdateAt<P_UnzonedSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<P_AllowSpawnSystem>(SystemUpdatePhase.Modification3);
            updateSystem.UpdateAt<P_BlockDeleteCleanupSystem>(SystemUpdatePhase.Modification4);
            updateSystem.UpdateAt<P_RoadConnectionSystem>(SystemUpdatePhase.Modification4B);
            updateSystem.UpdateAt<P_ParcelToBlockReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_BlockToRoadReferenceSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_ParcelSearchSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<P_ParcelBlockClassifySystem>(SystemUpdatePhase.ModificationEnd);

            // UI/Rendering
            updateSystem.UpdateAt<P_UISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_ParcelInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_BuildingInfoPanelSystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<P_OverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<P_TooltipSystem>(SystemUpdatePhase.UITooltip);

            // Tools
            updateSystem.UpdateBefore<P_SnapSystem>(SystemUpdatePhase.Modification1);
            // P_GenerateZonesSystem needs to run before GenerateZonesSystem
            updateSystem.UpdateBefore<P_GenerateZonesSystem, GenerateZonesSystem>(SystemUpdatePhase.Modification1); 
            // Important to run P_NewCellCheckSystem after Mod5 (When CellCheckSystem runs) so that all buffers and states are synced
            updateSystem.UpdateBefore<P_NewCellCheckSystem>(SystemUpdatePhase.ModificationEnd); 

            // Tests
            #if IS_DEBUG
            updateSystem.UpdateAt<P_TestToolSystem>(SystemUpdatePhase.ToolUpdate);
            #endif
        }

        /// <inheritdoc/>
        protected override void OnAfterLoad(UpdateSystem updateSystem) {
            ModifyVabillaSubBlockSerialization(updateSystem.World.GetOrCreateSystemManaged<SubBlockSystem>());
            RegisterCustomInputActions();
        }

#if IS_DEBUG && EXPORT_EN_US
        /// <inheritdoc/>
        protected override void GenerateLanguageFile() {
            ModLog.Debug("GenerateLanguageFile()");
            var localeDict = new EnUsConfig(Settings).ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>())
                                                     .ToDictionary(pair => pair.Key, pair => pair.Value);
            var str = JsonConvert.SerializeObject(localeDict, Formatting.Indented);
            try {
                var path       = GetThisFilePath();
                var directory  = Path.GetDirectoryName(path);
                var exportPath = $@"{directory}/L10n/lang/en-US.json";
                File.WriteAllText(exportPath, str);
            } catch (Exception ex) {
                ModLog.Error(ex.ToString());
            }
        }

        private static string GetThisFilePath([CallerFilePath] string path = null) { return path; }
#endif

        /// <summary>
        /// Registers custom input actions for parcel manipulation (depth, width, and setback adjustments).
        /// This is needed because the modding API doesn't allow creating mousewheel-based shortcuts directly.
        /// </summary>
        private void RegisterCustomInputActions() {
            ModLog.Debug("RegisterCustomInputActions()");

            RegisterCustomScrollAction(
                "BlockDepthAction",
                new[]
                {
                    new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
                });
            RegisterCustomScrollAction(
                "BlockWidthAction",
                new[]
                {
                    new Tuple<string, string>("alt", "<Keyboard>/alt"),
                });
            RegisterCustomScrollAction(
                "BlockSizeAction",
                new[]
                {
                    new Tuple<string, string>("alt", "<Keyboard>/alt"),
                    new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
                });
            RegisterCustomScrollAction(
                "SetbackAction",
                new[]
                {
                    new Tuple<string, string>("ctrl", "<Keyboard>/ctrl"),
                    new Tuple<string, string>("shift", "<Keyboard>/shift"),
                });
        }

        /// <summary>
        /// Registers a custom scroll-based input action with the specified name and modifier keys.
        /// </summary>
        /// <param name="name">The name of the action to register.</param>
        /// <param name="modifiers">Array of modifier key tuples (name, path) to apply to the action.</param>
        private void RegisterCustomScrollAction(string name, Tuple<string, string>[] modifiers) {
            var preciseRotation = InputManager.instance.FindAction("Tool", "Precise Rotation");
            if (preciseRotation == null) {
                ModLog.Error("RegisterCustomInputActions() -- Could not find Precise Rotation action");
                return;
            }

            var composites = preciseRotation.composites;

            var blockWidthCustomAction = new ProxyAction.Info
            {
                m_Name = name,
                m_Map  = "Platter.Platter.PlatterMod",
                m_Type = ActionType.Vector2,
                m_Composites = composites
                               .Where(keyValuePair => keyValuePair.Key == InputManager.DeviceType.Mouse)
                               .Select(keyValuePair => {
                    var device    = keyValuePair.Key;
                    var composite = keyValuePair.Value;
                    var source    = (CompositeInstance)composite.GetMemberValue("m_Source");
                    source.builtIn = false;

                    // Get the original bindings and modify them to use Alt modifier only
                    var modifiedBindings = composite.bindings.Values
                                                    .Select(binding => {
                                                        return binding.WithModifiers(
                                                            modifiers.Select(m => new ProxyModifier
                                                            {
                                                                m_Component = binding.component,
                                                                m_Name      = m.Item1,
                                                                m_Path      = m.Item2,
                                                            }).ToList());
                                                    }).ToList();

                    return new ProxyComposite.Info
                    {
                        m_Device   = device,
                        m_Source   = source,
                        m_Bindings = modifiedBindings,
                    };
                }).ToList(),
            };

            // Use reflection extension to call the internal AddActions instance method
            if (!InputManager.instance.TryInvokeMethod("AddActions", out _, new[] { blockWidthCustomAction })) {
                ModLog.Error("RegisterCustomInputActions() -- Could not find or invoke InputManager.AddActions method");
                return;
            }

            ModLog.Debug($"RegisterCustomInputActions() -- Registered custom action {name}");
        }

        /// <summary>
        /// Modifies the vanilla SubBlockSystem's entity query to exclude parcels from serialization.
        /// </summary>
        /// <param name="originalSystem">The SubBlockSystem to modify.</param>
        private void ModifyVabillaSubBlockSerialization(ComponentSystemBase originalSystem) {
            ModLog.Debug("ModifyVabillaSubBlockSerialization()");

            // get original system's EntityQuery
            var queryField = typeof(SubBlockSystem).GetField("m_Query", BindingFlags.Instance | BindingFlags.NonPublic);
            if (queryField == null) {
                ModLog.Error("ModifyVabillaSubBlockSerialization() -- Could not find m_Query for compatibility patching");
                return;
            }

            var originalQuery = (EntityQuery)queryField.GetValue(originalSystem);

            if (originalQuery.GetHashCode() == 0) {
                ModLog.Error("ModifyVabillaSubBlockSerialization() -- SubBlockSystem was not initialized!");
            }

            var originalQueryDescs = originalQuery.GetEntityQueryDescs();
            var componentType      = ComponentType.ReadOnly<ParcelOwner>();

            foreach (var originalQueryDesc in originalQueryDescs) {
                if (originalQueryDesc.None.Contains(componentType)) {
                    continue;
                }

                // add Parcel to force vanilla skip all entities with the Parcel component
                originalQueryDesc.None = originalQueryDesc.None.Append(componentType).ToArray();
                var getQueryMethod = typeof(ComponentSystemBase).GetMethod(
                    "GetEntityQuery",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    CallingConventions.Any,
                    new[] { typeof(EntityQueryDesc[]) },
                    Array.Empty<ParameterModifier>()
                );

                // generate EntityQuery
                var modifiedQuery = (EntityQuery)getQueryMethod.Invoke(originalSystem, new object[] { new[] { originalQueryDesc } });

                // replace current query to use more restrictive
                queryField.SetValue(originalSystem, modifiedQuery);

                // add EntityQuery to update check
                originalSystem.RequireForUpdate(modifiedQuery);
            }

            ModLog.Debug("ModifyVabillaSubBlockSerialization() -- Patching complete.");
        }
    }
}