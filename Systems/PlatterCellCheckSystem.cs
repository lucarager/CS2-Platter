// <copyright file="PlatterCellCheckSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Game;
    using Game.Common;
    using Game.Tools;
    using Game.Zones;
    using Platter.Components;
    using Unity.Entities;

    internal partial class PlatterCellCheckSystem : GameSystemBase {
        private EntityQuery m_Query;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Query = GetEntityQuery(new ComponentType[]
            {
                ComponentType.ReadOnly<Block>(),
                ComponentType.ReadOnly<ParcelOwner>(),
                ComponentType.ReadOnly<Updated>(),
                ComponentType.Exclude<Temp>()
            });
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // todo
        }
    }
}
