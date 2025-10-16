using HarmonyLib;

namespace FirestoneCardsRenderer
{
    public static class DialogManagerPatcher
    {
        [HarmonyPatch(typeof(DialogManager), "ShowReconnectHelperDialog")]
        [HarmonyPrefix]
        public static bool ShowReconnectHelperDialogPrefix()
        {
            return false;
        }
    }

    public static class EntityDefPatcher
    {
        [HarmonyPatch(typeof(EntityDef), "IsValidEntityName")]
        [HarmonyPostfix]
        public static void IsValidEntityNameSuffix(ref bool __result)
        {
            __result = false;
        }
    }
}
