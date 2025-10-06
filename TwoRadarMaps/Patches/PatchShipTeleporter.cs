using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

using HarmonyLib;
using Unity.Netcode;

using TwoRadarMaps.Utilities.IL;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    internal static class PatchShipTeleporter
    {
        private static readonly List<uint> rpcMessageIDs = new(2);

        private static bool useOldRPCHandlerTable = false;

        internal static void Apply()
        {
            Plugin.Harmony.PatchAll(typeof(PatchShipTeleporter));

            var initializeRpcs = typeof(ShipTeleporter).GetMethod("__initializeRpcs", BindingFlags.NonPublic | BindingFlags.Instance);

            if (initializeRpcs == null)
            {
                initializeRpcs = typeof(ShipTeleporter).GetMethod("InitializeRPCS_ShipTeleporter", BindingFlags.NonPublic | BindingFlags.Static);
                useOldRPCHandlerTable = true;
            }

            Plugin.Harmony
                .CreateProcessor(initializeRpcs)
                .AddPostfix(typeof(PatchShipTeleporter).GetMethod(nameof(PatchRPCReceiveHandlers), BindingFlags.NonPublic | BindingFlags.Static))
                .Patch();
        }

        private static bool TryGetRPCHandler(Type type, uint hash, out Delegate handler)
        {
            if (useOldRPCHandlerTable)
            {
                handler = null;
                if (!NetworkManager.__rpc_func_table.TryGetValue(hash, out var oldHandler))
                    return false;
                handler = oldHandler;
                return true;
            }
            return TryGetRPCHandlerNew(type, hash, out handler);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryGetRPCHandlerNew(Type type, uint hash, out Delegate handler)
        {
            handler = null;
            if (!NetworkBehaviour.__rpc_func_table.TryGetValue(type, out var handlers))
                return false;
            if (!handlers.TryGetValue(hash, out var oldHandler))
                return false;
            handler = oldHandler;
            return true;
        }

        private static void PatchRPCReceiveHandlers()
        {
            var m_TranspileRPCReceiveHandlerToSetTarget = typeof(PatchShipTeleporter).GetMethod(nameof(TranspileRPCReceiveHandlerToSetTarget), BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var rpcMessageID in rpcMessageIDs)
            {
                if (!TryGetRPCHandler(typeof(ShipTeleporter), rpcMessageID, out var rpcDelegate))
                {
                    Plugin.Instance.Logger.LogError($"Failed to find RPC message handler with ID {rpcMessageID}.");
                    Plugin.Instance.Logger.LogError("This may cause errors when using the ship teleporter.");
                    continue;
                }
                Plugin.Harmony.CreateProcessor(rpcDelegate.GetMethodInfo())
                    .AddTranspiler(m_TranspileRPCReceiveHandlerToSetTarget)
                    .Patch();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipTeleporter.OnEnable))]
        private static void OnEnablePostfix(ShipTeleporter __instance)
        {
            if (__instance.isInverseTeleporter)
                return;
            Plugin.Teleporter = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipTeleporter.OnDisable))]
        private static void OnDisablePostfix(ShipTeleporter __instance)
        {
            if (__instance != Plugin.Teleporter)
                return;
            Plugin.Teleporter = null;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(ShipTeleporter.PressTeleportButtonServerRpc))]
        [HarmonyPatch(nameof(ShipTeleporter.PressTeleportButtonClientRpc))]
        private static IEnumerable<CodeInstruction> TranspileRPCSendToWriteTargetIndex(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            //   FastBufferWriter bufferWriter = __beginSendServerRpc(389447712u, serverRpcParams, RpcDelivery.Reliable);
            // + WriteCurrentTarget(bufferWriter);
            //   __endSendServerRpc(ref bufferWriter, 389447712u, serverRpcParams, RpcDelivery.Reliable);
            var injector = new ILInjector(instructions)
                .Find([
                    ILMatcher.Ldarg(0),
                    ILMatcher.Ldc().CaptureOperandAs(out int messageID),
                    ILMatcher.Ldloc(),
                    ILMatcher.Ldc(),
                    ILMatcher.Predicate(insn => insn.opcode == OpCodes.Call && ((MethodBase)insn.operand).Name.StartsWith("__beginSend")),
                    ILMatcher.Stloc().CaptureAs(out var storeWriter),
                ])
                .GoToMatchEnd();

            if (!injector.IsValid)
            {
                Plugin.Instance.Logger.LogError($"No call to __beginSend[..]() was found in {method.DeclaringType.Name}.{method.Name}().");
                return instructions;
            }

            injector
                .Insert([
                    storeWriter.StlocToLdloc(),
                    new CodeInstruction(OpCodes.Call, typeof(PatchShipTeleporter).GetMethod(nameof(WriteCurrentTargetIndex), BindingFlags.NonPublic | BindingFlags.Static)),
                ]);
            rpcMessageIDs.Add((uint)messageID);
            return injector.ReleaseInstructions();
        }

        private static void WriteCurrentTargetIndex(FastBufferWriter writer)
        {
            writer.WriteValue(StartOfRound.Instance.mapScreen.targetTransformIndex);
        }

        private static int ReadTargetIndexAndSet(FastBufferReader reader)
        {
            var mapRenderer = StartOfRound.Instance.mapScreen;
            var oldIndex = mapRenderer.targetTransformIndex;
            reader.ReadValue(out int targetIndex);
            Plugin.SetTargetIndex(mapRenderer, targetIndex);
            return oldIndex;
        }

        private static IEnumerable<CodeInstruction> TranspileRPCReceiveHandlerToSetTarget(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
        {
            //   NetworkManager networkManager = target.NetworkManager;
            //   if (networkManager is null || !networkManager.IsListening)
            //       return;
            // + var originalIndex = ReadTargetIndexAndSet(reader);
            //   ...
            // + SetTarget(originalIndex);

            var injector = new ILInjector(instructions)
                .Find([
                    ILMatcher.Ldloc(),
                    ILMatcher.Callvirt(typeof(NetworkManager).GetProperty(nameof(NetworkManager.IsListening)).GetMethod),
                    ILMatcher.Opcode(OpCodes.Brtrue).CaptureOperandAs(out Label isListeningLabel),
                ])
                .FindLabel(isListeningLabel);

            if (!injector.IsValid)
            {
                Plugin.Instance.Logger.LogError($"No access of NetworkManager.IsListening was found in {method.DeclaringType.Name}.{method.Name}().");
                return instructions;
            }

            var originalIndexLocal = generator.DeclareLocal(typeof(int));
            injector
                .InsertAfterBranch([
                    new(OpCodes.Ldarg_1),
                    new(OpCodes.Call, typeof(PatchShipTeleporter).GetMethod(nameof(ReadTargetIndexAndSet), BindingFlags.NonPublic | BindingFlags.Static)),
                    new(OpCodes.Stloc, originalIndexLocal),
                ])
                .Find([
                    ILMatcher.Opcode(OpCodes.Ret),
                ]);

            if (!injector.IsValid)
            {
                Plugin.Instance.Logger.LogError($"No return instruction was found in {method.DeclaringType.Name}.{method.Name}().");
                return instructions;
            }

            return injector
                .Insert([
                    new(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                    new(OpCodes.Ldfld, Reflection.f_StartOfRound_mapScreen),
                    new(OpCodes.Ldloc, originalIndexLocal),
                    new(OpCodes.Call, typeof(Plugin).GetMethod(nameof(Plugin.SetTargetIndex), BindingFlags.NonPublic | BindingFlags.Static)),
                ])
                .ReleaseInstructions();
        }
    }
}
