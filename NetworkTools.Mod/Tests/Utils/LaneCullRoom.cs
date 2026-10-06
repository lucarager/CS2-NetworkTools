namespace NetworkTools.Tests {
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;

    using Game.Simulation;

    using HarmonyLib;

    using Unity.Entities;

    /// <summary>
    ///     Gives the game's list of terrain lane sections room for every section its roads can add.
    ///     <c>TerrainSystem.CullForCascades</c> makes room for six sections a road edge.
    ///     Its <c>CullRoadsJob</c> adds up to thirty through a writer that never grows or checks.
    ///     Past the capacity the job writes over the next block of the heap.
    ///     The game dies later, when the list grows and frees its old buffer.
    ///     The fault is the game's, reported in <see cref="Report" />; the proving ground hits it.
    ///     Arming throws when the game no longer sizes the list that way, fixed or changed.
    /// </summary>
    internal static class LaneCullRoom {
        /// <summary>
        ///     The bug report of the overrun, named in the error when the game has changed.
        /// </summary>
        private const string Report =
            "https://forum.paradoxplaza.com/forum/threads/the-game-crashes-with-an-access-violation-in-unityplayer-dll-shortly-after-roads-are-built-with-no-error-or-log-line-the-cause-is-a-buffer-overrun.1942353/";

        /// <summary>
        ///     Most lane sections one road edge adds.
        ///     The edge adds two segments and each end node up to four.
        ///     Each segment adds up to three sections.
        /// </summary>
        private const int SectionsAnEdge = 30;

        private static Harmony s_Harmony;

        /// <summary>
        ///     Patches the game's sizing of the list, once for the game session.
        ///     The code is checked before it is patched.
        ///     A change in the game then fails with this class's message rather than Harmony's.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     The game no longer sizes the list for six sections a road edge.
        /// </exception>
        public static void Arm() {
            if (s_Harmony != null) {
                return;
            }

            var cull = AccessTools.Method(typeof(TerrainSystem), "CullForCascades");

            if (cull == null) {
                throw Changed("TerrainSystem.CullForCascades is gone");
            }

            FindSizing(PatchProcessor.GetOriginalInstructions(cull));

            s_Harmony = new Harmony("NetworkTools.Tests.LaneCullRoom");
            s_Harmony.Patch(
                cull,
                transpiler: new HarmonyMethod(typeof(LaneCullRoom), nameof(Transpile)));
        }

        /// <summary>
        ///     Makes the game count thirty sections a road edge where it counts six.
        /// </summary>
        /// <param name="instructions">The code of <c>CullForCascades</c>.</param>
        /// <returns>The code, patched.</returns>
        private static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions) {
            var code  = new List<CodeInstruction>(instructions);
            var sixes = FindSizing(code);

            // In place, so that a label on the instruction stays on it.
            code[sixes].opcode  = OpCodes.Ldc_I4;
            code[sixes].operand = SectionsAnEdge;

            return code;
        }

        /// <summary>
        ///     Finds where the game multiplies the count of road edges by six.
        ///     It must be the only place the method multiplies an entity count by six.
        /// </summary>
        /// <param name="instructions">The code of <c>CullForCascades</c>.</param>
        /// <returns>Index of the six.</returns>
        /// <exception cref="InvalidOperationException">
        ///     The six is not there, or not there once.
        /// </exception>
        private static int FindSizing(IEnumerable<CodeInstruction> instructions) {
            var count = AccessTools.Method(
                typeof(EntityQuery),
                nameof(EntityQuery.CalculateEntityCountWithoutFiltering),
                Type.EmptyTypes);

            var code  = new List<CodeInstruction>(instructions);
            var found = -1;

            for (var i = 0; i + 2 < code.Count; i++) {
                if (!code[i].Calls(count)
                    || code[i + 1].opcode != OpCodes.Ldc_I4_6
                    || code[i + 2].opcode != OpCodes.Mul) {
                    continue;
                }

                if (found >= 0) {
                    throw Changed("it multiplies an entity count by six more than once");
                }

                found = i + 1;
            }

            if (found < 0) {
                throw Changed("it no longer makes room for six lane sections a road edge");
            }

            return found;
        }

        /// <summary>
        ///     Makes the error of a game that has changed, pointing to what to check.
        /// </summary>
        /// <param name="what">What changed.</param>
        /// <returns>The error.</returns>
        private static InvalidOperationException Changed(string what) {
            return new InvalidOperationException(
                "The game has changed how TerrainSystem.CullForCascades sizes m_LaneCullList: "
                + $"{what}. "
                + $"Check whether the overrun reported at {Report} is fixed, "
                + $"then remove or rewrite {nameof(LaneCullRoom)}.");
        }
    }
}
