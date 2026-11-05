using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace RaftDurabilityX10
{
    /// <summary>
    /// All tool / weapon durability loss in Raft funnels through two methods on
    /// PlayerInventory:
    ///
    ///   RemoveDurabillityFromHotSlot(int uses)                (hotbar tools, weapons, fishing rod, hook, ...)
    ///   RemoveDurabillityFromEquipment(EquipSlotType, int)    (equipped gear, e.g. zipline hook)
    ///
    /// Vanilla body:
    ///   if (tools.areToolsIndestructible) return false;
    ///   int loss = (int)(uses * tools.toolDurabilityLossMultiplier);
    ///   return RemoveStacksFromSlot(slot, loss);              (or ...FromHotSlot)
    ///
    /// We replace that with the same logic but divide the loss by the configured
    /// multiplier, carrying the fractional remainder on the ItemInstance so the
    /// total lifetime becomes exactly N times longer (a tool loses 1 point per
    /// 10 swings at multiplier 10). Save data, item definitions and the UI are
    /// left untouched, so existing saves keep working.
    /// </summary>
    internal static class DurabilityPatches
    {
        // Fractional durability that has not been applied yet, per item instance.
        private static readonly ConditionalWeakTable<ItemInstance, StrongBox<float>> Remainders =
            new ConditionalWeakTable<ItemInstance, StrongBox<float>>();

        private static MethodInfo _removeStacksFromSlot;
        private static bool _removeStacksFromSlotResolved;

        /// <summary>Private PlayerInventory.RemoveStacksFromSlot(Slot, int) - the game's own damage applier.</summary>
        private static MethodInfo RemoveStacksFromSlotMethod()
        {
            if (!_removeStacksFromSlotResolved)
            {
                _removeStacksFromSlotResolved = true;

                _removeStacksFromSlot = AccessTools.Method(
                    typeof(PlayerInventory),
                    "RemoveStacksFromSlot",
                    new[] { typeof(Slot), typeof(int) });

                if (_removeStacksFromSlot == null)
                {
                    DurabilityPlugin.Log.LogWarning(
                        "PlayerInventory.RemoveStacksFromSlot(Slot, int) was not found. " +
                        "The mod will fall back to vanilla durability loss.");
                }
            }

            return _removeStacksFromSlot;
        }

        [HarmonyPatch(typeof(PlayerInventory), "RemoveDurabillityFromHotSlot")]
        private static class HotSlotPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(PlayerInventory __instance, int durabilityStacksToRemove, ref bool __result)
            {
                return TryApplyDamage(
                    __instance,
                    __instance.GetSelectedHotbarSlot(),
                    durabilityStacksToRemove,
                    ref __result);
            }
        }

        [HarmonyPatch(typeof(PlayerInventory), "RemoveDurabillityFromEquipment")]
        private static class EquipmentPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(
                PlayerInventory __instance,
                EquipSlotType equipmentType,
                int durabilityStacksToRemove,
                ref bool __result)
            {
                if (DurabilityPlugin.AffectEquipment != null && !DurabilityPlugin.AffectEquipment.Value)
                {
                    return true; // let vanilla handle it
                }

                return TryApplyDamage(
                    __instance,
                    __instance.GetEquipmentSlotFromEquipmentType(equipmentType),
                    durabilityStacksToRemove,
                    ref __result);
            }
        }

        /// <summary>
        /// Applies a scaled-down durability loss. Returns false when the vanilla
        /// method must be skipped (we already produced the result), true to let
        /// the original method run unchanged.
        /// </summary>
        private static bool TryApplyDamage(PlayerInventory inventory, Slot slot, int amount, ref bool result)
        {
            float multiplier = DurabilityPlugin.DurabilityMultiplier != null
                ? DurabilityPlugin.DurabilityMultiplier.Value
                : 1f;

            if (multiplier <= 1f)
            {
                return true; // vanilla behaviour
            }

            if (inventory == null || slot == null || slot.IsEmpty || amount <= 0)
            {
                return true; // vanilla no-ops anyway
            }

            MethodInfo removeStacks = RemoveStacksFromSlotMethod();
            if (removeStacks == null)
            {
                return true;
            }

            // Honour the game mode settings exactly like the vanilla method does.
            SO_GameModeValue gameMode = GameModeValueManager.GetCurrentGameModeValue();
            ToolVariables tools = gameMode != null ? gameMode.toolVariables : null;

            if (tools != null && tools.areToolsIndestructible)
            {
                result = false;
                return false;
            }

            float vanillaLoss = tools != null ? amount * tools.toolDurabilityLossMultiplier : amount;
            if (vanillaLoss <= 0f)
            {
                result = false;
                return false;
            }

            int loss = ConsumeWholeUses(slot.itemInstance, vanillaLoss / multiplier);
            if (loss <= 0)
            {
                result = false; // nothing to apply yet, keep the remainder
                return false;
            }

            try
            {
                result = (bool)removeStacks.Invoke(inventory, new object[] { slot, loss });
                LogFirstHit(slot, loss);
            }
            catch (Exception e)
            {
                DurabilityPlugin.Log.LogError("Failed to apply scaled durability loss: " + e);
                result = false;
            }

            return false;
        }

        /// <summary>
        /// Diagnostics: how many of our prefixes Harmony attached to each target
        /// method. Expect "hotSlot=1, equipment=1".
        /// </summary>
        internal static string DescribePatchState()
        {
            return string.Format(
                "hotSlot={0}, equipment={1}",
                CountOurPrefixes(AccessTools.Method(typeof(PlayerInventory), "RemoveDurabillityFromHotSlot")),
                CountOurPrefixes(AccessTools.Method(typeof(PlayerInventory), "RemoveDurabillityFromEquipment")));
        }

        private static int CountOurPrefixes(MethodInfo method)
        {
            if (method == null)
            {
                return -1; // target method missing
            }

            Patches info = Harmony.GetPatchInfo(method);
            if (info == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Patch patch in info.Prefixes)
            {
                if (patch.owner == PluginInfo.Guid)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool _firstHitLogged;

        private static void LogFirstHit(Slot slot, int loss)
        {
            if (_firstHitLogged)
            {
                return;
            }

            _firstHitLogged = true;

            ItemInstance item = slot != null ? slot.itemInstance : null;
            string itemName = "unknown";
            if (item != null && item.baseItem != null)
            {
                itemName = item.baseItem.UniqueName;
            }

            DurabilityPlugin.Log.LogInfo(string.Format(
                "First scaled durability loss applied: item={0}, consumed={1} durability point(s), multiplier={2}x.",
                itemName,
                loss,
                DurabilityPlugin.DurabilityMultiplier.Value));
        }

        /// <summary>
        /// Adds <paramref name="rawUses"/> to the item's pending remainder and
        /// returns how many whole durability points should be consumed now.
        /// </summary>
        private static int ConsumeWholeUses(ItemInstance item, float rawUses)
        {
            if (item == null)
            {
                return (int)rawUses;
            }

            StrongBox<float> box;
            if (!Remainders.TryGetValue(item, out box))
            {
                box = new StrongBox<float>(0f);
                Remainders.Add(item, box);
            }

            box.Value += rawUses;

            if (box.Value < 1f)
            {
                return 0;
            }

            int whole = (int)Math.Floor(box.Value);
            box.Value -= whole;
            return whole;
        }
    }
}
