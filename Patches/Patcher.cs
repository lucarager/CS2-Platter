// <copyright file="Patcher.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Patches {
    using System;
    using Colossal.Logging;
    using HarmonyLib;

    /// <summary>
    /// A basic Harmony patching class.
    /// </summary>
    public class Patcher {
        private readonly string _harmonyID;

        /// <summary>
        /// Initializes a new instance of the <see cref="Patcher"/> class.
        /// Doing so applies all annotated patches.
        /// </summary>
        /// <param name="harmonyID">Harmony ID to use.</param>
        /// <param name="log">Log to use for performing patching.</param>
        public Patcher(string harmonyID, ILog log) {
            // Set log reference.
            Log = log;

            // Dispose of any existing instance.
            if (Instance != null) {
                log.Error("[Patcher] Existing Patcher instance detected with ID " + Instance._harmonyID + "; reverting");
                Instance.UnPatchAll();
            }

            // Set instance reference.
            Instance = this;
            _harmonyID = harmonyID;

            // Apply annotated patches.
            PatchAnnotations();
        }

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        public static Patcher Instance {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether patches were successfully applied.
        /// </summary>
        public bool PatchesApplied { get; private set; } = false;

        /// <summary>
        /// Gets the logger to use when patching.
        /// </summary>
        public ILog Log {
            get; private set;
        }

        /// <summary>
        /// Reverts all applied patches.
        /// </summary>
        public void UnPatchAll() {
            if (!string.IsNullOrEmpty(_harmonyID)) {
                Log.Info("[Patcher]  Reverting all applied patches for " + _harmonyID);
                Harmony harmonyInstance = new(_harmonyID);

                try {
                    harmonyInstance.UnpatchAll("_harmonyID");

                    // Clear applied flag.
                    PatchesApplied = false;
                } catch (Exception e) {
                    Log.Critical(e, "[Patcher] Exception reverting all applied patches for " + _harmonyID);
                }
            }
        }

        /// <summary>
        /// Constructor.
        /// Applies Harmony patches.
        /// </summary>
        private void PatchAnnotations() {
            Log.Info("[Patcher] Applying annotated Harmony patches for " + _harmonyID);
            Harmony harmonyInstance = new(_harmonyID);

            try {
                harmonyInstance.PatchAll();
                var patchedMethods = harmonyInstance.GetPatchedMethods();
                foreach (var patchedMethod in patchedMethods) {
                    Log.Info($"[Patcher] Patched method: {patchedMethod.Module.Name}:{patchedMethod.Name}");
                }

                Log.Info("[Patcher] Patching complete");

                // Set applied flag.
                PatchesApplied = true;
            } catch (Exception e) {
                Log.Critical(e, "[Patcher] Exception applying annotated Harmony patches; reverting");
                harmonyInstance.UnpatchAll(_harmonyID);
            }
        }
    }
}
