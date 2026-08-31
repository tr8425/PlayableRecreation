using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Stargazing
{
    /// <summary>지표의 한 칸. 색을 정하는 데 필요한 것만 담는다.</summary>
    public struct GroundCell
    {
        /// <summary>바로 아래를 원점으로 한 -1..1 좌표. 위가 북쪽, 오른쪽이 동쪽이다.</summary>
        public Vector2 At;

        public float Warm;
        public float Wet;
        public float High;

        public bool Water;

        /// <summary>그 자리는 지금 낮인가. 경도가 다르면 시각도 다르다.</summary>
        public bool Lit;

        public string Label;

        /// <summary>정착지나 지형지물이 있으면 여기에 이름이 들어온다.</summary>
        public string Mark;

        public bool Center;
    }

    /// <summary>망원경을 아래로 돌렸을 때 보이는 것.</summary>
    public class GroundPatch
    {
        public bool Valid;

        /// <summary>이웃한 칸 사이의 간격. 칸을 얼마나 크게 그릴지가 여기서 나온다.</summary>
        public float Spacing = 0.34f;

        public string BelowLabel;
        public string BelowRegion;
        public bool BelowLit;
        public float BelowHour;

        public readonly List<GroundCell> Cells = new List<GroundCell>();
    }

    /// <summary>
    /// 궤도에서 아래를 본다.
    ///
    /// 궤도 타일은 행성 위 어딘가의 200km 상공이므로, 바로 밑에는 늘 지표 타일이 하나 있다.
    /// 그것과 이웃들을 접평면에 눌러 펴면 그대로 아래에 펼쳐진 땅이 된다 -
    /// 지어낸 지형이 아니라 이 세계가 실제로 가지고 있는 타일들이다.
    /// </summary>
    public static class GroundWatch
    {
        /// <summary>바로 아래에서 몇 칸까지 볼 것인가. 망원경이지 위성 사진이 아니다.</summary>
        private const int Rings = 3;

        public static GroundPatch Observe(Map map)
        {
            GroundPatch patch = new GroundPatch();
            if (map == null || Find.WorldGrid == null) return patch;

            PlanetLayer surface = Find.WorldGrid.Surface;
            if (surface == null || !map.Tile.Valid) return patch;

            // _NewTemp 은 게임이 직접 가리키는 쪽이다. validSettlement 는 정착지만 고를 때 쓰므로 끈다.
            PlanetTile below = surface.GetClosestTile_NewTemp(map.Tile, false);
            if (!below.Valid) return patch;

            Vector2 origin = surface.LongLatOf(below);
            float shrink = Mathf.Cos(origin.y * Mathf.Deg2Rad);
            if (shrink < 0.05f) shrink = 0.05f;

            List<PlanetTile> near = Spread(surface, below);

            // 먼저 접평면 좌표를 다 뽑아 두고, 가장 먼 것이 테두리에 닿도록 함께 줄인다.
            List<Vector2> flat = new List<Vector2>(near.Count);
            float far = 0.0001f;

            for (int i = 0; i < near.Count; i++)
            {
                Vector2 longLat = surface.LongLatOf(near[i]);

                Vector2 offset = new Vector2(
                    WrapDegrees(longLat.x - origin.x) * shrink,
                    -(longLat.y - origin.y));

                flat.Add(offset);
                if (offset.magnitude > far) far = offset.magnitude;
            }

            long ticks = Find.TickManager != null ? Find.TickManager.TicksAbs : 0L;

            for (int i = 0; i < near.Count; i++)
                patch.Cells.Add(Read(surface, near[i], flat[i] / far, near[i] == below, ticks));

            patch.Spacing = SpacingOf(patch.Cells);
            patch.Valid = patch.Cells.Count > 0;

            GroundCell middle = patch.Cells[0];
            patch.BelowLabel = middle.Label;
            patch.BelowLit = middle.Lit;
            patch.BelowHour = GenDate.HourFloat(ticks, origin.x);

            Tile info = surface[below];
            if (info != null && info.feature != null) patch.BelowRegion = info.feature.name;

            return patch;
        }

        /// <summary>바로 아래에서 바깥으로 몇 겹. 첫 항목은 언제나 한가운데다.</summary>
        private static List<PlanetTile> Spread(PlanetLayer surface, PlanetTile middle)
        {
            List<PlanetTile> found = new List<PlanetTile> { middle };
            HashSet<int> seen = new HashSet<int> { middle.tileId };

            List<PlanetTile> edge = new List<PlanetTile> { middle };
            List<PlanetTile> next = new List<PlanetTile>();
            List<PlanetTile> buffer = new List<PlanetTile>();

            for (int ring = 0; ring < Rings; ring++)
            {
                next.Clear();

                for (int i = 0; i < edge.Count; i++)
                {
                    buffer.Clear();
                    surface.GetTileNeighbors(edge[i], buffer);

                    for (int j = 0; j < buffer.Count; j++)
                    {
                        if (!buffer[j].Valid || !seen.Add(buffer[j].tileId)) continue;

                        found.Add(buffer[j]);
                        next.Add(buffer[j]);
                    }
                }

                edge.Clear();
                edge.AddRange(next);
            }

            return found;
        }

        private static GroundCell Read(PlanetLayer surface, PlanetTile tile, Vector2 at,
                                       bool center, long ticks)
        {
            Tile info = surface[tile];

            GroundCell cell = new GroundCell { At = at, Center = center };
            if (info == null) return cell;

            cell.Water = info.WaterCovered;
            cell.Warm = Mathf.InverseLerp(-25f, 30f, info.temperature);
            cell.Wet = Mathf.InverseLerp(0f, 1800f, info.rainfall);
            cell.High = Mathf.InverseLerp(0f, 1500f, info.elevation);

            if (info.PrimaryBiome != null) cell.Label = info.PrimaryBiome.LabelCap;

            float longitude = surface.LongLatOf(tile).x;
            float hour = GenDate.HourFloat(ticks, longitude);
            cell.Lit = hour >= 6f && hour < 18f;

            cell.Mark = MarkOn(tile, info);
            return cell;
        }

        /// <summary>여기 무엇이 있는가. 사람이 사는 곳이 먼저고, 없으면 눈에 띄는 지형이다.</summary>
        private static string MarkOn(PlanetTile tile, Tile info)
        {
            if (Find.WorldObjects != null)
            {
                foreach (WorldObject world in Find.WorldObjects.ObjectsAt(tile))
                {
                    if (world == null) continue;
                    return world.LabelShortCap;
                }
            }

            Landmark landmark = info.Landmark;
            if (landmark == null) return null;

            return !landmark.name.NullOrEmpty() ? landmark.name
                 : (landmark.def != null ? landmark.def.LabelCap.ToString() : null);
        }

        /// <summary>한가운데에서 가장 가까운 이웃까지. 칸 하나를 그 간격에 맞춰 그린다.</summary>
        private static float SpacingOf(List<GroundCell> cells)
        {
            float best = float.MaxValue;

            for (int i = 1; i < cells.Count; i++)
            {
                float gap = (cells[i].At - cells[0].At).magnitude;
                if (gap > 0.0001f && gap < best) best = gap;
            }

            return best < float.MaxValue ? best : 0.34f;
        }

        private static float WrapDegrees(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;

            return degrees;
        }
    }
}
