namespace Platter.Patches {
    using System;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Tools;
    using Game.UI.InGame;
    using HarmonyLib;
    using Unity.Entities;
    using static Game.Rendering.Debug.RenderPrefabRenderer;

    [HarmonyPatch]
    internal class AreaToolSystemPatches {

        [HarmonyPatch(typeof(AreaToolSystem), "InitializeRaycast")]
        public static void PostFix(AreaToolSystem __instance) {
            if (__instance.prefab != null) {
                Mod.Instance.Log.Debug(__instance.prefab.name);
            }
        }
    }
}
