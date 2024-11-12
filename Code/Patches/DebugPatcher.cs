namespace Platter
{
    using System;
    using Game.UI.InGame;
    using HarmonyLib;
    using Unity.Collections;
    using Unity.Entities;

    [HarmonyPatch]
    [HarmonyPatch]
    public class DevUIListComponents
    {

        [HarmonyPatch(typeof(DeveloperInfoUISystem), "OnCreate")]
        [HarmonyPostfix]
        public static void AddComponentsList(ref DeveloperInfoUISystem __instance, ref SelectedInfoUISystem ___m_InfoUISystem)
        {
            var em = __instance.EntityManager;
            Action<Entity, Entity, InfoList> updateInfoMethod = (Entity entity, Entity prefab, InfoList info) =>
            {
                info.label = "Instance ECS Components";
                NativeArray<ComponentType> arr = em.GetChunk(entity).Archetype.GetComponentTypes(Allocator.Temp);
                for (int i = 0; i < arr.Length; i++)
                {
                    var ct = arr[i];
                    info.Add(new InfoList.Item(ct.GetManagedType().FullName));
                }
            };
            ___m_InfoUISystem.AddDeveloperInfo(new InfoList(
                (entity1, entity2) => true, updateInfoMethod
            ));
        }
    }
}
