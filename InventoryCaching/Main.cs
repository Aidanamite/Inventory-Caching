using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using HMLLibrary;

public class InventoryCaching : Mod
{
    Harmony harmony;
    public void Start()
    {
        (harmony = new Harmony("com.aidanamite.InventoryCaching")).PatchAll();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
        Log("Mod has been unloaded!");
    }
    static List<(Inventory, string, int)> itemCache = new List<(Inventory, string, int)>();
    public static bool TryGetItemCount(Inventory inventory, string item, out int amount)
    {
        foreach (var t in itemCache)
            if (t.Item1 && t.Item1 == inventory && item == t.Item2)
            {
                amount = t.Item3;
                return true;
            }
        amount = 0;
        return false;
    }
    public static void CacheItem(Inventory inventory, string item, int amount) => itemCache.Add((inventory, item, amount));
    public static void ClearItemCache(string item) => itemCache.RemoveAll(x => !x.Item1 || x.Item2 == item);
    public static void ClearItemCache() => itemCache.Clear();

    public static void ExtraSettingsAPI_ButtonPress(string name)
    {
        if (name == "clear")
            ClearItemCache();
    }
}

[HarmonyPatch(typeof(Inventory),"GetItemCount",typeof(Item_Base))]
static class Patch_GetItemCount
{
    static void Prefix(Inventory __instance, Item_Base item, ref int __result, ref bool __state)
    {
        if (!item || !InventoryCaching.TryGetItemCount(__instance, item.UniqueName, out var r))
            return;
        __result = r;
        __state = true;
        throw new SkipPostfixes();
    }
    static Exception Finalizer(Exception __exception, Inventory __instance, Item_Base item, int __result, bool __state)
    {
        if (__exception is SkipPostfixes)
            return null;
        if (__exception == null && !__state && item)
        {
            //Debug.Log("Cache created");
            InventoryCaching.CacheItem(__instance, item.UniqueName, __result);
        }
        return __exception;
    }
    class SkipPostfixes : Exception { }
}


[HarmonyPatch(typeof(ItemInstance))]
static class Patch_ItemInstance
{
    [HarmonyPatch("Amount", MethodType.Setter)]
    [HarmonyPrefix]
    static void set_Amount_Prefix(ref int __state, ItemInstance __instance) => __state = __instance.Amount;

    [HarmonyPatch("Amount", MethodType.Setter)]
    [HarmonyPostfix]
    static void set_Amount_Postfix(ref int __state, ItemInstance __instance)
    {
        if (__state != __instance.Amount)
            InventoryCaching.ClearItemCache(__instance.baseItem.UniqueName);
    }

    [HarmonyPatch(MethodType.Constructor, typeof(Item_Base), typeof(int), typeof(int), typeof(string))]
    [HarmonyPostfix]
    static void ctor_Postfix(ItemInstance __instance)
    {
        InventoryCaching.ClearItemCache(__instance.baseItem.UniqueName);
    }

    [HarmonyPatch("Clone")]
    [HarmonyPostfix]
    static void Clone_Postfix(ItemInstance __result)
    {
        InventoryCaching.ClearItemCache(__result.baseItem.UniqueName);
    }
}

[HarmonyPatch(typeof(Slot))]
static class Patch_Slot
{
    [HarmonyPatch("SetItem", typeof(ItemInstance))]
    [HarmonyPrefix]
    static void SetItem_Prefix(ItemInstance newInstance, Slot __instance)
    {
        if (__instance.itemInstance != null && __instance.itemInstance.Valid)
            InventoryCaching.ClearItemCache(__instance.itemInstance.baseItem.UniqueName);
        if (newInstance != null && newInstance.Valid)
            InventoryCaching.ClearItemCache(newInstance.baseItem.UniqueName);
    }

    [HarmonyPatch("SetItem",typeof(Item_Base), typeof(int))]
    [HarmonyPrefix]
    static void SetItem2_Prefix(Item_Base newItem, Slot __instance)
    {
        if (__instance.itemInstance != null && __instance.itemInstance.Valid)
            InventoryCaching.ClearItemCache(__instance.itemInstance.baseItem.UniqueName);
        if (newItem != null)
            InventoryCaching.ClearItemCache(newItem.UniqueName);
    }
    public class store<T> { public T value; public static implicit operator T(store<T> v) => v.value; public static implicit operator store<T>(T v) => new store<T>() { value = v }; }
    public static Dictionary<Slot, store<ItemInstance>> prevs = new Dictionary<Slot, store<ItemInstance>>();

    [HarmonyPatch("RefreshComponents")]
    [HarmonyPrefix]
    static void RefreshComponents_Prefix(Slot __instance)
    {
        if (prevs.TryGetValue(__instance, out var v))
        {
            if (v.value != __instance.itemInstance)
            {
                if (v.value?.baseItem?.UniqueName != null)
                    InventoryCaching.ClearItemCache(v.value.baseItem.UniqueName);
                if (__instance.itemInstance?.baseItem?.UniqueName != null)
                    InventoryCaching.ClearItemCache(__instance.itemInstance.baseItem.UniqueName);
                v.value = __instance.itemInstance;
            }
        }
        else
            prevs[__instance] = __instance.itemInstance;

    }
}