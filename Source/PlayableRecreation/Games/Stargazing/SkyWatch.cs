using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Stargazing.Core;
using UnityEngine;
using Verse;

namespace Stargazing
{
    /// <summary>궤도에 떠 있는 것 하나. 오디세이가 있을 때만 하늘에 올라온다.</summary>
    public struct SkyObject
    {
        public string Label;
        public float Ra;
        public float Dec;

        /// <summary>배인가, 바위인가.</summary>
        public bool Craft;
    }

    /// <summary>
    /// 지금 이 자리에서 하늘이 어떻게 보이는가. 위도·경도·시각·날씨를 한 번에 읽어 온다.
    ///
    /// 위도는 <see cref="Map.Tile"/> 에서 그때그때 읽는다 - 중력선이 움직이면 타일이 바뀌고,
    /// 그러면 별도 따라 바뀐다. 이 기능이 오디세이와 맞물리는 자리는 여기 한 줄이다.
    /// </summary>
    public class SkyView
    {
        public float LatitudeRad;
        public float LongitudeDeg;
        public float Sidereal;

        /// <summary>이 등급까지 보인다. 낮이거나 흐리면 크게 내려간다.</summary>
        public float LimitMagnitude = StarField.FaintestMagnitude;

        public float Glow;
        public bool InSpace;
        public int DayOfYear;
        public float Hour;
        public Season Season;
        public string WeatherLabel;
        public List<SkyObject> Objects = new List<SkyObject>();

        /// <summary>이 세계에 위성 궤도층이 있는가. 오디세이가 없으면 언제나 false.</summary>
        public bool HasMoon;
        public float MoonRa;
        public float MoonDec;

        /// <summary>0이 신월, 1이 보름. 위상은 궤도 위치에서 그대로 나온다.</summary>
        public float MoonLit;

        public bool Clear { get { return LimitMagnitude > 3.5f; } }
    }

    public static class SkyWatch
    {
        /// <summary>맑은 밤에 맨눈으로 보이는 한계.</summary>
        private const float DarkLimit = StarField.FaintestMagnitude;

        /// <summary>대기가 없으면 여기까지 보인다.</summary>
        private const float SpaceLimit = 7.2f;

        /// <summary>달이 한 바퀴 도는 데 걸리는 날. 며칠에 한 번은 보름이어야 한다.</summary>
        private const float MoonPeriodDays = 8f;

        public static SkyView Observe(Thing board)
        {
            SkyView view = new SkyView();

            Map map = board != null ? board.Map : Find.CurrentMap;
            if (map == null) return view;

            PlanetTile tile = map.Tile;

            if (Find.WorldGrid != null && tile.Valid)
            {
                Vector2 longLat = Find.WorldGrid.LongLatOf(tile);
                view.LongitudeDeg = longLat.x;
                view.LatitudeRad = longLat.y * SkyMath.Deg2Rad;
            }

            view.InSpace = tile.Valid && tile.LayerDef != null && tile.LayerDef.isSpace;

            view.DayOfYear = GenLocalDate.DayOfYear(map);
            view.Hour = GenLocalDate.HourFloat(map);
            view.Season = GenLocalDate.Season(map);

            view.Sidereal = SkyMath.SiderealTime(
                view.DayOfYear / (float)GenDate.DaysPerYear,
                view.Hour / GenDate.HoursPerDay,
                view.LongitudeDeg);

            view.Glow = map.skyManager != null ? map.skyManager.CurSkyGlow : 0f;
            view.LimitMagnitude = LimitFor(map, view);

            if (map.weatherManager != null && map.weatherManager.curWeather != null)
                view.WeatherLabel = map.weatherManager.curWeather.LabelCap;

            CollectObjects(map, view);
            LocateMoon(view);
            return view;
        }

        /// <summary>
        /// 무엇까지 보이는가. 해가 떠 있으면 가장 밝은 몇 개만 남고, 비가 오면 더 줄어든다.
        /// 별이 없어진 것이 아니라 안 보이는 것이다 - 이 값 하나가 그 차이를 만든다.
        /// </summary>
        private static float LimitFor(Map map, SkyView view)
        {
            if (view.InSpace) return SpaceLimit;
            if (StargazingSettings.IgnoreConditions) return DarkLimit;

            float limit = DarkLimit - view.Glow * 5.8f;

            if (map.weatherManager != null)
            {
                limit -= map.weatherManager.RainRate * 3.4f;
                limit -= map.weatherManager.SnowRate * 3.4f;
            }

            return Mathf.Clamp(limit, -2f, DarkLimit);
        }

        /// <summary>
        /// 달. 오디세이가 위성 궤도층을 등록해 둔 세계에만 뜬다 -
        /// 그 층이 없는 행성에는 달도 없다는 뜻이고, 그게 맞다.
        ///
        /// 위상은 따로 굴리지 않는다. 궤도 위 어디에 있느냐가 그대로 얼마나 차 있느냐다.
        /// </summary>
        private static void LocateMoon(SkyView view)
        {
            if (!ModsConfig.OdysseyActive || Find.WorldGrid == null) return;

            bool found = false;
            foreach (KeyValuePair<int, PlanetLayer> pair in Find.WorldGrid.PlanetLayers)
            {
                PlanetLayer layer = pair.Value;
                if (layer == null || layer.Def == null) continue;
                if (layer.IsRootSurface || layer.Def == PlanetLayerDefOf.Orbit) continue;

                found = true;
                break;
            }

            if (!found) return;

            int ticks = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            float cycle = ticks / (float)GenDate.TicksPerDay / MoonPeriodDays % 1f;

            view.HasMoon = true;
            view.MoonRa = SkyMath.Wrap(cycle * SkyMath.TwoPi);
            view.MoonDec = 0.35f * (float)System.Math.Sin(cycle * SkyMath.TwoPi);

            // 태양 반대편에 있을 때가 보름이다. 한 바퀴가 곧 한 삭망월이 된다.
            view.MoonLit = 0.5f - 0.5f * (float)System.Math.Cos(cycle * SkyMath.TwoPi);
        }

        /// <summary>
        /// 궤도에 있는 것들. 정확한 궤도 요소를 흉내 내지는 않는다 -
        /// 타일 번호에서 뽑은 고정된 자리에 하루 몇 바퀴의 흐름을 얹은 것이다.
        /// 대신 위도에 따라 뜨고 지는 것만은 별과 똑같이 다룬다.
        /// </summary>
        private static void CollectObjects(Map map, SkyView view)
        {
            if (!ModsConfig.OdysseyActive) return;
            if (Find.WorldObjects == null) return;

            List<WorldObject> all = Find.WorldObjects.AllWorldObjects;
            int ticks = Find.TickManager != null ? Find.TickManager.TicksGame : 0;

            // 하루에 세 바퀴. 창을 열어 두면 실제로 흘러가는 것이 보인다.
            float drift = ticks / (float)GenDate.TicksPerDay * SkyMath.TwoPi * 3f;

            for (int i = 0; i < all.Count; i++)
            {
                WorldObject world = all[i];
                if (world == null || !world.Tile.Valid) continue;

                PlanetLayer layer = world.Tile.Layer;
                if (layer == null || layer.IsRootSurface) continue;
                if (world.Tile == map.Tile) continue;   // 지금 서 있는 곳은 하늘이 아니다

                int hash = Gen.HashCombineInt(world.Tile.tileId, 0x5BF03635);

                view.Objects.Add(new SkyObject
                {
                    Label = world.LabelShortCap,
                    Ra = SkyMath.Wrap((hash & 0xFFFF) / 65535f * SkyMath.TwoPi + drift),
                    Dec = ((hash >> 16 & 0xFFFF) / 65535f - 0.5f) * 1.9f,
                    Craft = world.def != null && world.def.defName.Contains("Gravship"),
                });
            }
        }
    }
}
