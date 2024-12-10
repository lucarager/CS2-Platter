// <copyright file="OverlaySystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Platter.Systems {
    using Colossal.Mathematics;
    using Game.Prefabs;
    using Game.Rendering;
    using Platter.Components;
    using Platter.Utils;
    using System;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class OverlaySystem : SystemBase {
        private OverlayRenderSystem m_OverlayRenderSystem;

        // Logger
        private PrefixedLogger m_Log;

        // Queries
        private EntityQuery m_ParcelQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(OverlaySystem));
            m_Log.Debug($"OnCreate()");

            // Queries
            m_ParcelQuery = GetEntityQuery(
                new EntityQueryDesc {
                    All = new ComponentType[] {
                        ComponentType.ReadOnly<Parcel>()
                    },
                });

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            try {
                if (Camera.main is null) {
                    throw new NullReferenceException("Camera.main is null");
                }

                OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle overlayRenderBufferHandle);

                DrawOverlaysJob drawOverlays = new() {
                    m_OverlayRenderBuffer = buffer,
                    m_CameraPosition = (float3)Camera.main.transform.position,
                    m_TransformComponentTypeHandle = GetComponentTypeHandle<Game.Objects.Transform>(),
                    m_PrefabRefComponentTypeHandle = GetComponentTypeHandle<PrefabRef>(),
                    m_ObjectGeometryComponentLookup = GetComponentLookup<ObjectGeometryData>(),
                };
                JobHandle drawMoveableHandle = drawOverlays.ScheduleByRef(m_ParcelQuery, overlayRenderBufferHandle);

                m_OverlayRenderSystem.AddBufferWriter(drawMoveableHandle);
                drawMoveableHandle.Complete();
                Dependency = drawMoveableHandle;
            } catch (Exception ex) {
                m_Log.Error($"Failed on DrawOverlaysJob:\n{ex}");
            }
        }

        internal static Line3.Segment[] GetBoundsLines(float4x4 trs, ObjectGeometryData objectGeoData) {
            var center = MathUtils.Center(objectGeoData.m_Bounds);
            var size = MathUtils.Size(objectGeoData.m_Bounds);
            var halfSize = size * 0.5f;

            // Bottom and Top: Indicate whether the corner is on the bottom or top face of the cube.
            // Left and Right: Indicate whether the corner is on the left or right side when viewed from the front.
            // Front and Back: Indicate whether the corner is at the front or back of the cube.
            var cornerBottomLeftFront = math.transform(trs, center + (new float3(-1f, -1f, -1f) * halfSize));
            var cornerBottomLeftBack = math.transform(trs, center + (new float3(-1f, -1f, 1f) * halfSize));
            var cornerBottomRightFront = math.transform(trs, center + (new float3(1f, -1f, -1f) * halfSize));
            var cornerBottomRightBack = math.transform(trs, center + (new float3(1f, -1f, 1f) * halfSize));
            var cornerTopLeftFront = math.transform(trs, center + (new float3(-1f, 1f, -1f) * halfSize));
            var cornerTopLeftBack = math.transform(trs, center + (new float3(-1f, 1f, 1f) * halfSize));
            var cornerTopRightFront = math.transform(trs, center + (new float3(1f, 1f, -1f) * halfSize));
            var cornerTopRightBack = math.transform(trs, center + (new float3(1f, 1f, 1f) * halfSize));

            return new Line3.Segment[] {
                // Bottom face edges
                new (cornerBottomLeftFront, cornerBottomRightFront),
                new (cornerBottomRightFront, cornerBottomRightBack),
                new (cornerBottomRightBack, cornerBottomLeftBack),
                new (cornerBottomLeftBack, cornerBottomLeftFront),

                // Top face edges
                // new (cornerTopLeftFront, cornerTopRightFront),
                // new (cornerTopRightFront, cornerTopRightBack),
                // new (cornerTopRightBack, cornerTopLeftBack),
                // new (cornerTopLeftBack, cornerTopLeftFront),

                // Vertical edges connecting top and bottom faces
                // new (cornerBottomLeftFront, cornerTopLeftFront),
                // new (cornerBottomRightFront, cornerTopRightFront),
                // new (cornerBottomRightBack, cornerTopRightBack),
                // new (cornerBottomLeftBack, cornerTopLeftBack)
            };
        }

        internal static Line3.Segment[] GetFrontCircle(float4x4 trs, ObjectGeometryData objectGeoData) {
            var center = MathUtils.Center(objectGeoData.m_Bounds);
            var size = MathUtils.Size(objectGeoData.m_Bounds);
            var halfSize = size * 0.5f;
            var lineWidth = 1f;

            // Bottom and Top: Indicate whether the corner is on the bottom or top face of the cube.
            // Left and Right: Indicate whether the corner is on the left or right side when viewed from the front.
            // Front and Back: Indicate whether the corner is at the front or back of the cube.
            var cornerBottomLeftFront = math.transform(trs, center + (new float3(-1f, -1f, -1f) * halfSize) + lineWidth);
            var cornerBottomLeftBack = math.transform(trs, center + (new float3(-1f, -1f, 1f) * halfSize) + lineWidth);
            var cornerBottomRightFront = math.transform(trs, center + (new float3(1f, -1f, -1f) * halfSize) + lineWidth);
            var cornerBottomRightBack = math.transform(trs, center + (new float3(1f, -1f, 1f) * halfSize) + lineWidth);
            var cornerTopLeftFront = math.transform(trs, center + (new float3(-1f, 1f, -1f) * halfSize) + lineWidth);
            var cornerTopLeftBack = math.transform(trs, center + (new float3(-1f, 1f, 1f) * halfSize) + lineWidth);
            var cornerTopRightFront = math.transform(trs, center + (new float3(1f, 1f, -1f) * halfSize) + lineWidth);
            var cornerTopRightBack = math.transform(trs, center + (new float3(1f, 1f, 1f) * halfSize) + lineWidth);

            return new Line3.Segment[] {
                // Bottom face edges
                new (cornerBottomLeftFront, cornerBottomRightFront),
                new (cornerBottomRightFront, cornerBottomRightBack),
                new (cornerBottomRightBack, cornerBottomLeftBack),
                new (cornerBottomLeftBack, cornerBottomLeftFront),

                // Top face edges
                // new (cornerTopLeftFront, cornerTopRightFront),
                // new (cornerTopRightFront, cornerTopRightBack),
                // new (cornerTopRightBack, cornerTopLeftBack),
                // new (cornerTopLeftBack, cornerTopLeftFront),

                // Vertical edges connecting top and bottom faces
                // new (cornerBottomLeftFront, cornerTopLeftFront),
                // new (cornerBottomRightFront, cornerTopRightFront),
                // new (cornerBottomRightBack, cornerTopRightBack),
                // new (cornerBottomLeftBack, cornerTopLeftBack)
            };
        }

        protected struct DrawOverlaysJob : IJobChunk {
            [ReadOnly] public OverlayRenderSystem.Buffer m_OverlayRenderBuffer;
            [ReadOnly] public float3 m_CameraPosition;
            [ReadOnly] public ComponentTypeHandle<Game.Objects.Transform> m_TransformComponentTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefComponentTypeHandle;
            [ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryComponentLookup;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask) {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var transformData = chunk.GetNativeArray(ref m_TransformComponentTypeHandle);
                var prefabRefData = chunk.GetNativeArray(ref m_PrefabRefComponentTypeHandle);

                while (enumerator.NextEntityIndex(out var i)) {
                    var transform = transformData[i];
                    var prefabRef = prefabRefData[i];

                    if (!m_ObjectGeometryComponentLookup.TryGetComponent(prefabRef, out var objectGeoData)) {
                        return;
                    }

                    // Combines the translation part of the trs matrix (c3.xyz) with the local center to calculate the cube's world position.
                    var trs = new float4x4(transform.m_Rotation, transform.m_Position);
                    var center = MathUtils.Center(objectGeoData.m_Bounds);
                    var size = MathUtils.Size(objectGeoData.m_Bounds);
                    var worldPosition = trs.c3.xyz + center;
                    var lines = GetBoundsLines(trs, objectGeoData);
                    var color = new UnityEngine.Color(1f, 1f, 1f, 1f);
                    var frontPosition = math.transform(trs, center + (new float3(0, -1f, 1f) * size / 2));

                    foreach (var line in lines) {
                        m_OverlayRenderBuffer.DrawLine(color, line, 1f);
                    }

                    m_OverlayRenderBuffer.DrawCircle(color, frontPosition, 4f);
                }
            }
        }
    }
}
