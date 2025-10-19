// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace Platter.Components {
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct TransformUpdated : IComponentData {
    }
}

