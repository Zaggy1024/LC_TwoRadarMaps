using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using Unity.Netcode;

namespace TwoRadarMaps.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    internal static class PatchShipTeleporter
    {
        public static readonly MethodInfo m_NetworkBehaviour___beginSendServerRpc = typeof(NetworkBehaviour).GetMethod("__beginSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(uint), typeof(ServerRpcParams), typeof(RpcDelivery)]);
        public static readonly MethodInfo m_NetworkBehaviour___beginSendClientRpc = typeof(NetworkBehaviour).GetMethod("__beginSendClientRpc", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(uint), typeof(ClientRpcParams), typeof(RpcDelivery)]);

        public static readonly MethodInfo m_NetworkManager_get_IsListening = typeof(NetworkManager).GetMethod("get_IsListening");

        public static readonly MethodInfo m_WriteCurrentTarget = typeof(PatchShipTeleporter).GetMethod(nameof(WriteCurrentTargetIndex), BindingFlags.NonPublic | BindingFlags.Static, [typeof(FastBufferWriter)]);
        public static readonly MethodInfo m_ReadTargetIndexAndSet = typeof(PatchShipTeleporter).GetMethod(nameof(ReadTargetIndexAndSet), BindingFlags.NonPublic | BindingFlags.Static, [typeof(FastBufferReader)]);

        public static readonly MethodInfo m_Plugin_SetTargetIndex = typeof(Plugin).GetMethod(nameof(Plugin.SetTargetIndex), BindingFlags.NonPublic | BindingFlags.Static, [typeof(ManualCameraRenderer), typeof(int), typeof(bool)]);

        public static readonly MethodInfo m_ShipTeleporter_PressTeleportButtonServerRpcHandler = typeof(ShipTeleporter).GetMethod(nameof(ShipTeleporter.__rpc_handler_389447712), BindingFlags.NonPublic | BindingFlags.Static, [typeof(NetworkBehaviour), typeof(FastBufferReader), typeof(__RpcParams)]);
        public static readonly MethodInfo m_ShipTeleporter_PressTeleportButtonClientRpc = typeof(ShipTeleporter).GetMethod(nameof(ShipTeleporter.PressTeleportButtonClientRpc), []);

        private static readonly List<uint> rpcMessageIDs = new(2);

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
            var instructionsList = instructions.ToList();

            var beginSend = instructionsList.FindIndex(insn => insn.Calls(m_NetworkBehaviour___beginSendServerRpc) || insn.Calls(m_NetworkBehaviour___beginSendClientRpc));
            if (beginSend == -1)
            {
                Plugin.Instance.Logger.LogError($"No call to __beginSendServerRpc() was found.");
                return instructions;
            }

            var rpcMessageIDInsns = instructionsList.InstructionRangeForStackItems(beginSend, 2, 2);
            if (rpcMessageIDInsns is null
                || rpcMessageIDInsns.Size != 1
                || !instructionsList[rpcMessageIDInsns.Start].LoadsConstant())
            {
                Plugin.Instance.Logger.LogError($"No RPC message ID was found in {method}.");
                return instructions;
            }
            rpcMessageIDs.Add((uint)(int)instructionsList[rpcMessageIDInsns.Start].operand);

            //   FastBufferWriter bufferWriter = __beginSendServerRpc(389447712u, serverRpcParams, RpcDelivery.Reliable);
            // + WriteCurrentTarget(bufferWriter);
            //   __endSendServerRpc(ref bufferWriter, 389447712u, serverRpcParams, RpcDelivery.Reliable);
            instructionsList.InsertRange(beginSend + 1,
                [
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Call, m_WriteCurrentTarget),
                ]);

            return instructionsList;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipTeleporter.InitializeRPCS_ShipTeleporter))]
        private static void InitializeRPCS_ShipTeleporterPostfix()
        {
            PatchRPCReceiveHandlers(Plugin.Harmony);
        }

        private static void WriteCurrentTargetIndex(FastBufferWriter writer)
        {
            writer.WriteValue(StartOfRound.Instance.mapScreen.targetTransformIndex);
        }

        private static void PatchRPCReceiveHandlers(Harmony harmony)
        {
            var transpilerMethod = typeof(PatchShipTeleporter).GetMethod(nameof(TranspileRPCReceiveHandlerToSetTarget), BindingFlags.NonPublic | BindingFlags.Static, [typeof(IEnumerable<CodeInstruction>), typeof(ILGenerator), typeof(MethodBase)]);

            foreach (var rpcMessageID in rpcMessageIDs)
            {
                var handlerMethod = NetworkManager.__rpc_func_table[rpcMessageID].GetMethodInfo();
                harmony.CreateProcessor(handlerMethod)
                    .AddTranspiler(transpilerMethod)
                    .Patch();
            }
        }

        private static IEnumerable<CodeInstruction> TranspileRPCReceiveHandlerToSetTarget(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
        {
            var instructionsList = instructions.ToList();

            var checkIsListening = instructionsList.FindIndexOfSequence([
                insn => insn.IsLdloc(),
                insn => insn.Calls(m_NetworkManager_get_IsListening),
                insn => insn.opcode == OpCodes.Brtrue || insn.opcode == OpCodes.Brtrue_S,
            ]);
            var isListeningLabel = (Label)instructionsList[checkIsListening.End - 1].operand;

            var isListening = instructionsList.FindIndex(checkIsListening.End, insn => insn.labels.Contains(isListeningLabel));
            var endListeningBlock = instructionsList.FindIndex(isListening, insn => insn.opcode == OpCodes.Ret);

            // NetworkManager networkManager = target.NetworkManager;
            // if (networkManager is null || !networkManager.IsListening)
            //     return;
            // var originalIndex = ReadTargetIndexAndSet(reader);
            // ...
            // SetTarget(originalIndex);

            instructionsList[isListening].labels.Remove(isListeningLabel);

            var originalIndexVar = generator.DeclareLocal(typeof(int));
            CodeInstruction[] insertAtStart = [
                new CodeInstruction(OpCodes.Ldarg_1).WithLabels(isListeningLabel),
                new CodeInstruction(OpCodes.Call, m_ReadTargetIndexAndSet),
                new CodeInstruction(OpCodes.Stloc, originalIndexVar),
            ];
            instructionsList.InsertRange(isListening, insertAtStart);
            endListeningBlock += insertAtStart.Length;

            CodeInstruction[] insertAtEnd = [
                new CodeInstruction(OpCodes.Call, Reflection.m_StartOfRound_Instance),
                new CodeInstruction(OpCodes.Ldfld, Reflection.f_StartOfRound_mapScreen),
                new CodeInstruction(OpCodes.Ldloc, originalIndexVar),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Call, m_Plugin_SetTargetIndex),
            ];
            instructionsList.InsertRange(endListeningBlock, insertAtEnd);

            return instructionsList;
        }

        private static int ReadTargetIndexAndSet(FastBufferReader reader)
        {
            var mapRenderer = StartOfRound.Instance.mapScreen;
            var oldIndex = mapRenderer.targetTransformIndex;
            reader.ReadValue(out int targetIndex);
            Plugin.SetTargetIndex(mapRenderer, targetIndex, setText: false);
            return oldIndex;
        }
    }
}
