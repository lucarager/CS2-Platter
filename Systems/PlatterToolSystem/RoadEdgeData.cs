// <copyright file="RoadEdgeData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    public class RoadEdgeData : PlatterToolModeData {
        /// <summary>
        /// Initializes a new instance of the <see cref="RoadEdgeData"/> class.
        /// </summary>
        public RoadEdgeData() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RoadEdgeData"/> class.
        /// </summary>
        /// <param name="mode"></param>
        public RoadEdgeData(PlatterToolModeData mode)
            : base(mode) {
        }
    }
}
