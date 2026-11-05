using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace RaftDurabilityX10
{
    /// <summary>
    /// BepInEx entry point. Makes every tool / weapon that uses the game's
    /// "durability" (Uses) system last 10x longer by dividing the durability
    /// loss per use, instead of touching item definitions or save data.
    /// </summary>
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("Raft.exe")]
    public sealed class DurabilityPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<float> DurabilityMultiplier;
        internal static ConfigEntry<bool> AffectEquipment;

        private void Awake()
        {
            Log = Logger;

            DurabilityMultiplier = Config.Bind(
                "General",
                "DurabilityMultiplier",
                10f,
                "How many times longer tools last compared to vanilla. " +
                "10 = 10x durability (one durability point is consumed every 10 uses). " +
                "1 = vanilla behaviour (mod effectively disabled).");

            AffectEquipment = Config.Bind(
                "General",
                "AffectEquipment",
                true,
                "Also slow down durability loss for equipped gear that uses the equipment " +
                "durability path (for example the zipline hook).");

            if (DurabilityMultiplier.Value < 1f)
            {
                DurabilityMultiplier.Value = 1f;
            }

            Harmony harmony = new Harmony(PluginInfo.Guid);

            // PatchAll(Type) only looks at methods declared directly on that type, so the
            // annotated nested patch classes are picked up by scanning this assembly instead.
            try
            {
                harmony.PatchAll(typeof(DurabilityPlugin).Assembly);
            }
            catch (System.Exception e)
            {
                Log.LogError("Failed to apply durability patches: " + e);
            }

            string patchState = DurabilityPatches.DescribePatchState();
            Log.LogInfo(string.Format(
                "{0} v{1} loaded. Tools now last {2:0.##}x longer (durability loss divided by {2:0.##}).",
                PluginInfo.Name,
                PluginInfo.Version,
                DurabilityMultiplier.Value));
            Log.LogInfo("Harmony patch state: " + patchState + " (expected: hotSlot=1, equipment=1).");

            if (patchState != "hotSlot=1, equipment=1")
            {
                Log.LogError("A durability patch did not attach - the mod is NOT active. " +
                             "The game may have been updated and the target methods changed.");
            }
        }
    }

    internal static class PluginInfo
    {
        internal const string Guid = "raft.durability.x10";
        internal const string Name = "Raft Durability x10";
        internal const string Version = "1.0.0";
    }
}
