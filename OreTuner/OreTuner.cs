using System;
using HarmonyLib;
using UnityEngine;

namespace OreTuner
{
    /// <summary>
    /// 7 Days To Die game modification.
    /// </summary>
    public class OreTuner : IModApi
    {
        /// <summary>
        /// Mod initialization.
        /// </summary>
        /// <param name="_modInstance"></param>
        public void InitMod(Mod _modInstance)
        {
            if (!Settings.ModIsEnabled)
            {
                return;
            }

            Debug.Log("Loading mod: " + GetType().ToString());
            var harmony = new Harmony(GetType().ToString());

            if (Settings.OreVeinsAmount < 100f && Settings.OreVeinsAmount >= 0)
            {
                OreLimit = Settings.OreVeinsAmount / 100f;
                if (Settings.OreVeinsAmount > 0)
                {
                    harmony.Patch(AccessTools.Constructor(typeof(HeightMap), new Type[] { typeof(int), typeof(int), typeof(float), typeof(IBackedArray<ushort>), typeof(int) }), null,
                        new HarmonyMethod(SymbolExtensions.GetMethodInfo((HeightMap __instance) => HeightMap_Constructor.Postfix(__instance))));
                }

                harmony.Patch(AccessTools.Method(typeof(GameUtils), nameof(GameUtils.GetOreNoiseAt)), null,
                    new HarmonyMethod(SymbolExtensions.GetMethodInfo((GameUtils_GetOreNoiseAt.APostfix p) => GameUtils_GetOreNoiseAt.Postfix(ref p.__result, p._x, p._y, p._z))));

                Debug.Log($"Mod {nameof(OreTuner)}: Ore veins limit applied: {Settings.OreVeinsAmount}%");
            }
            else if (Settings.OreVeinsAmount != 100f)
            {
                Debug.LogException(new Exception($"Mod {nameof(OreTuner)}: Invalid value '{Settings.OreVeinsAmount}' for setting '{nameof(Settings.OreVeinsAmount)}'. Should be number in range 0 - 100 (%)."));
            }

            if (!Settings.BoulderOnTheSurface)
            {
                harmony.Patch(AccessTools.Method(typeof(Chunk), nameof(Chunk.SetBlock), new Type[] { typeof(WorldBase), typeof(int), typeof(int), typeof(int), typeof(BlockValue), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int) }),
                    new HarmonyMethod(SymbolExtensions.GetMethodInfo((BlockValue _blockValue) => Chunk_SetBlock.Prefix(ref _blockValue))));
            }

            if (!Settings.ColoredDotOnTheMap)
            {
                harmony.Patch(AccessTools.Method(typeof(Block), nameof(Block.GetMapColor)), null,
                    new HarmonyMethod(SymbolExtensions.GetMethodInfo((Block_GetMapColor.APostfix p) => Block_GetMapColor.Postfix(p.__instance, ref p.__result))));
            }

            harmony.PatchAll();
        }

        private static float OreLimit = 1.0f;

        /// <summary>
        /// Gets the height of the terrain at the specified world coordinates.
        /// </summary>
        public static int GetTerrainHeight(int worldX, int worldZ)
        {
            if (HeightMap_Constructor.HeightMap != null)
            {
                var mx = worldX + HeightMap_Constructor.OffsetX;
                var mz = worldZ + HeightMap_Constructor.OffsetZ;
                float h = HeightMap_Constructor.HeightMap.GetAt(mx, mz) + 0.5f;
                return (int)h;
            }
            else
            {
                Debug.LogError($"Mod {nameof(OreTuner)}: Attempt to get terrain height value before the height map generated.");
            }
            return 0;
        }

        /// <summary>
        /// Keeps height map reference.
        /// </summary>
        public static class HeightMap_Constructor
        {
            public static HeightMap HeightMap = null;
            public static int OffsetX;
            public static int OffsetZ;

            public static void Postfix(HeightMap __instance)
            {
                HeightMap = __instance;
                OffsetX = (HeightMap.GetWidth() << HeightMap.GetScaleShift()) / 2;
                OffsetZ = (HeightMap.GetHeight() << HeightMap.GetScaleShift()) / 2;
            }
        }

        /// <summary>
        /// Prevents ore veins to reach surface depending on % chance from settings.
        /// </summary>
        public static class GameUtils_GetOreNoiseAt
        {
            static Vector2i prevChunk = Vector2i.zero;
            static int prevChunkHeight = 0;
            static Vector2 prevOrePos = Vector2.zero;

            public struct APostfix
            {
                public float __result;
                public int _x;
                public int _y;
                public int _z;
            }

            public static void Postfix(ref float __result, int _x, int _y, int _z)
            {
                if (Settings.OreVeinsAmount == 0f)
                {
                    __result = -1f;
                    return;
                }

                if (__result > 0f)
                {
                    var chunk = World.toChunkXZ(new Vector2i(_x, _z));
                    var height = GetTerrainHeight(_x, _z);

                    if (_y >= height - 12)
                    {
                        var random = GetRandomForPos(chunk.x, chunk.y);
                        if (random.RandomFloat > OreLimit)
                        {
                            __result = -1f;
                        }
                        else
                        {
                            // avoid multiple veins on surface in single chunk
                            if (chunk == prevChunk)
                            {
                                if (Mathf.Abs(_y - prevChunkHeight) < 5)
                                {
                                    var orePos = new Vector2(_x, _z);
                                    if (Vector2.Distance(prevOrePos, orePos) > 2f)
                                    {
                                        __result = -1f;
                                    }
                                }
                            }
                            else
                            {
                                prevChunk = chunk;
                                prevChunkHeight = _y;
                                prevOrePos = new Vector2(_x, _z);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Prevents placing ore boulders.
        /// </summary>
        public static class Chunk_SetBlock
        {
            public static bool Prefix(ref BlockValue _blockValue)
            {
                var name = _blockValue.Block?.GetBlockName();
                return name == null || !(name.StartsWith("ore") && name.EndsWith("Boulder"));
            }
        }

        /// <summary>
        /// Prevents using ore colors on the map.
        /// </summary>
        public static class Block_GetMapColor
        {
            static Color prevColor = new(0.47f, 0.47f, 0.47f); // grey rock

            public struct APostfix
            {
                public Block __instance;
                public Color __result;
            }

            public static void Postfix(Block __instance, ref Color __result)
            {
                var name = __instance.GetBlockName() ?? string.Empty;
                if (name.StartsWith("terrOre") || (name.StartsWith("ore") && name.EndsWith("Boulder")))
                {
                    __result = prevColor;
                    return;
                }
                prevColor = __result;
            }
        }

        /// <summary>
        /// Creates new <see cref="GameRandom"/> instance for the specified coordinates.
        /// </summary>
        public static GameRandom GetRandomForPos(int x, int z)
        {
            GameRandom gameRandom = null;
            if (GameManager.Instance.World != null)
            {
                gameRandom = Utils.RandomFromSeedOnPos(x, z, GameManager.Instance.World.Seed);
            }
            return gameRandom;
        }
    }
}