// <copyright file="ParcelUtils.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Utils {
    #region Using Statements

    using System;
    using Components;
    using Game.Prefabs;
    using Game.Zones;
    using Systems;
    using Unity.Burst;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    public static class DebugUtils {

        [BurstCompile]
        public static void DebugBlockStatus(string context, Entity blockEntity, DynamicBuffer<Game.Zones.Cell> cellBuffer) {
            //#if !USE_BURST
            //if (blockEntity == Entity.Null) {
            //    return;
            //}
            //var message = $"Block Entity: {blockEntity} -- Cells: {cellBuffer.Length}";
            //for (var i = 0; i < cellBuffer.Length; i++) {
            //    var cell = cellBuffer[i];
            //    message += $"\n - Cell {i}: m_Zone {cell.m_Zone} | m_State {cell.m_State}";
            //}
            //BurstLogger.Debug($"\n{context}", message);
            //#endif
        }
    }
}