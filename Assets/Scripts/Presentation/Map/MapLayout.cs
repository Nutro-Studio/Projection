using System;
using System.Collections.Generic;
using Core.Setup;
using UnityEngine;

namespace Presentation.Map
{
    public static class MapLayout
    {
        /// <summary>
        /// Computes an evenly spaced, non-overlapping node layout driven by the adjacency graph.
        /// Seeded West-to-East by province index so Militants stay West, Frontier in the middle, and Zealots East.
        /// </summary>
        public static Vector2[] CreateForceDirectedPositions(
            IReadOnlyList<ProvinceSeed> seeds,
            float targetEdgeLength = 5.4f,
            float minNodeDistance = 4.6f,
            int iterations = 350)
        {
            int n = seeds.Count;
            var pos = new Vector2[n];
            var disp = new Vector2[n];

            // Deterministic West-to-East initial placement to preserve faction geography and avoid tangled crossings
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0.5f;
                float x = Mathf.Lerp(-16f, 16f, t);
                float y = ((i % 3) - 1) * 4.2f + Mathf.Sin(i * 1.7f) * 1.5f;
                pos[i] = new Vector2(x, y);
            }

            float k = targetEdgeLength;
            float area = k * k * n;
            float maxStep = targetEdgeLength * 0.8f;

            for (int iter = 0; iter < iterations; iter++)
            {
                Array.Clear(disp, 0, n);
                float temp = maxStep * (1f - (float)iter / iterations);

                // Repulsive forces between all pairs
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        Vector2 delta = pos[i] - pos[j];
                        float dist = Mathf.Max(delta.magnitude, 0.01f);
                        Vector2 dir = delta / dist;

                        float force = (k * k) / dist;
                        if (dist < minNodeDistance)
                        {
                            // Strong short-range repulsion to guarantee zero overlap
                            force += (minNodeDistance - dist) * k * 2.5f;
                        }

                        Vector2 push = dir * force;
                        disp[i] += push;
                        disp[j] -= push;
                    }
                }

                // Attractive spring forces along graph edges
                for (int i = 0; i < n; i++)
                {
                    var neighbours = seeds[i].Neighbours;
                    for (int idx = 0; idx < neighbours.Count; idx++)
                    {
                        int j = neighbours[idx];
                        if (i >= j || j < 0 || j >= n) continue;

                        Vector2 delta = pos[i] - pos[j];
                        float dist = Mathf.Max(delta.magnitude, 0.01f);
                        Vector2 dir = delta / dist;

                        float force = (dist * dist) / k;
                        Vector2 pull = dir * force;
                        disp[i] -= pull;
                        disp[j] += pull;
                    }
                }

                // Gentle horizontal bias to keep West-to-East orientation readable
                for (int i = 0; i < n; i++)
                {
                    float targetX = Mathf.Lerp(-15f, 15f, n > 1 ? (float)i / (n - 1) : 0.5f);
                    disp[i].x += (targetX - pos[i].x) * 0.15f;
                    disp[i].y *= 0.92f; // Slightly flatten vertical aspect ratio for 16:9 screens
                }

                // Apply clamped displacement
                for (int i = 0; i < n; i++)
                {
                    float mag = disp[i].magnitude;
                    if (mag > 0.0001f)
                    {
                        pos[i] += (disp[i] / mag) * Mathf.Min(mag, temp);
                    }
                }
            }

            // Hard separation pass to strictly enforce minNodeDistance
            for (int pass = 0; pass < 40; pass++)
            {
                bool moved = false;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        Vector2 delta = pos[i] - pos[j];
                        float dist = delta.magnitude;
                        if (dist < minNodeDistance)
                        {
                            Vector2 dir = dist > 0.0001f ? delta / dist : Vector2.right;
                            float overlap = (minNodeDistance - dist) * 0.5f;
                            pos[i] += dir * overlap;
                            pos[j] -= dir * overlap;
                            moved = true;
                        }
                    }
                }

                if (!moved) break;
            }

            // Re-center around origin
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < n; i++) centroid += pos[i];
            centroid /= Mathf.Max(1, n);
            for (int i = 0; i < n; i++) pos[i] -= centroid;

            return pos;
        }
    }
}