// <copyright file="PrefabUIData.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.UI.Binding;

    /// <summary>
    /// Todo.
    /// </summary>
    public readonly struct PrefabUIData : IJsonWritable {
        private readonly string Name;
        private readonly string Thumbnail;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrefabUIData"/> struct.
        /// </summary>
        public PrefabUIData(string name, string thumbnail) {
            Name = name;
            Thumbnail = thumbnail;
        }

        /// <inheritdoc/>
        public readonly void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("name");
            writer.Write(Name);

            writer.PropertyName("thumbnail");
            writer.Write(Thumbnail);

            writer.TypeEnd();
        }
    }
}
