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
    /// 어디에 서서 보고 있는가. 이 셋은 서로 다른 하늘을 준다.
    ///
    /// 대기가 있느냐만으로는 모자란다 - 소행성과 정거장은 둘 다 진공이지만,
    /// 한쪽은 발밑에 바위가 있어 하늘의 절반이 가려지고 다른 쪽은 갑판뿐이다.
    /// </summary>
    public enum SkyPlace
    {
        /// <summary>행성 표면. 대기가 있고, 날씨가 있고, 지평선이 있다.</summary>
        Ground,

        /// <summary>소행성. 진공이지만 발밑이 바위다.</summary>
        Asteroid,

        /// <summary>궤도 구조물. 가릴 것이 없어 구 전체가 한 번에 보인다.</summary>
        Station,
    }

    /// <summary>
    /// 지금 이 자리에서 하늘이 어떻게 보이는가. 위도·경도·시각·날씨를 한 번에 읽어 온다.
    ///
    /// 위도는 <see cref="Map.Tile"/> 에서 그때그때 읽는다 - 중력선이 움직이면 타일이 바뀌고,
    /// 그러면 별도 따라 바뀐다. 이 기능이 오디세이와 맞물리는 자리는 여기 한 줄이다.
    /// </summary>
    public class SkyView
    {
        public SkyPlace Place = SkyPlace.Ground;

        public float LatitudeRad;
        public float LongitudeDeg;
        public float Sidereal;

        /// <summary>한 바퀴 중 어디까지 왔는가. 지상에서는 하루, 궤도에서는 한 공전이다.</summary>
        public float SpinFraction;

        /// <summary>이 등급까지 보인다. 낮이거나 흐리면 크게 내려간다.</summary>
        public float LimitMagnitude = StarField.FaintestMagnitude;

        public float Glow;
        public int DayOfYear;
        public float Hour;
        public Season Season;
        public string WeatherLabel;
        public List<SkyObject> Objects = new List<SkyObject>();

        /// <summary>해가 지금 어디 있는가. 명암 경계선은 전부 이 방향에서 나온다.</summary>
        public float SunAltitude;
        public float SunAzimuth;

        /// <summary>행성. 궤도에 올라와 있을 때만 하늘에 걸린다.</summary>
        public bool HasPlanet;
        public float PlanetAltitude;
        public float PlanetAzimuth;
        public float PlanetRadiusRad;
        public float PlanetLit;

        /// <summary>이 세계에 위성 궤도층이 있는가. 그런 층을 더하는 모드가 없으면 언제나 false.</summary>
        public bool HasMoon;
        public float MoonRa;
        public float MoonDec;

        /// <summary>0이 신월, 1이 보름. 위상은 궤도 위치에서 그대로 나온다.</summary>
        public float MoonLit;

        /// <summary>대기 밖인가. 소행성이든 정거장이든 여기서는 같다.</summary>
        public bool InSpace { get { return Place != SkyPlace.Ground; } }

        /// <summary>지평선이 있는가. 발밑에 무언가 있으면 하늘의 절반은 그것이 가린다.</summary>
        public bool HasHorizon { get { return Place != SkyPlace.Station; } }

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

        /// <summary>
        /// 궤도가 하루에 도는 횟수. 200km 상공이면 한 바퀴에 한 시간 반쯤이다 -
        /// 창을 열어 두면 하늘이 흘러가는 것이 눈에 보이고, 해도 그만큼 자주 뜬다.
        /// </summary>
        private const float OrbitsPerDay = 16f;

        /// <summary>
        /// 행성의 각지름. 200km 위에서는 140°에 이르지만 그대로 얹으면 하늘이 없어진다.
        /// 눌러 담되, 하늘에서 가장 큰 것이라는 사실만은 남긴다.
        /// </summary>
        private const float PlanetRadiusRad = 0.60f;

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

            view.Place = PlaceOf(map, tile);

            view.DayOfYear = GenLocalDate.DayOfYear(map);
            view.Hour = GenLocalDate.HourFloat(map);
            view.Season = GenLocalDate.Season(map);
            view.SpinFraction = SpinFor(view);

            view.Sidereal = SkyMath.SiderealTime(
                view.DayOfYear / (float)GenDate.DaysPerYear,
                view.SpinFraction,
                view.LongitudeDeg);

            SkyMath.SunAltAz(view.DayOfYear / (float)GenDate.DaysPerYear, view.SpinFraction,
                             view.LatitudeRad, out view.SunAltitude, out view.SunAzimuth);

            view.Glow = map.skyManager != null ? map.skyManager.CurSkyGlow : 0f;
            view.LimitMagnitude = LimitFor(map, view);

            if (map.weatherManager != null && map.weatherManager.curWeather != null)
                view.WeatherLabel = map.weatherManager.curWeather.LabelCap;

            LocatePlanet(view, tile);
            CollectObjects(map, view);
            LocateMoon(view);
            return view;
        }

        /// <summary>
        /// 발밑이 무엇인가. 진공인 것만으로는 소행성과 정거장이 갈리지 않는다 -
        /// 소행성 맵만 따로 쓰는 부모가 있어서 그것으로 가른다.
        /// </summary>
        private static SkyPlace PlaceOf(Map map, PlanetTile tile)
        {
            bool vacuum = tile.Valid && tile.LayerDef != null && tile.LayerDef.isSpace;
            if (!vacuum && map.Biome != null && map.Biome.inVacuum) vacuum = true;
            if (!vacuum) return SkyPlace.Ground;

            MapParent parent = map.Parent;
            if (parent is BasicAsteroidMapParent || parent is ResourceAsteroidMapParent)
                return SkyPlace.Asteroid;

            return SkyPlace.Station;
        }

        /// <summary>
        /// 하늘이 한 바퀴 도는 데 걸리는 시간. 지상에서는 하루지만 궤도에서는 한 공전이다 -
        /// 궤도에 올라가면 하늘이 눈에 띄게 빨라지는 이유가 이 한 줄이다.
        /// </summary>
        private static float SpinFor(SkyView view)
        {
            if (!view.InSpace) return view.Hour / GenDate.HoursPerDay;

            int ticks = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            float turns = ticks / (float)GenDate.TicksPerDay * OrbitsPerDay;

            return turns - Mathf.Floor(turns);
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
        /// 행성. 궤도에 올라오면 하늘에서 가장 큰 것은 별이 아니라 방금 떠나온 곳이다.
        ///
        /// 궤도 요소를 굴리지는 않는다 - 소행성도 정거장도 게임 안에서는 한자리에 있으므로,
        /// 행성도 그 자리에서 보면 늘 같은 쪽에 있다. 대신 차고 기우는 것은 진짜다.
        /// 해가 도는 대로 명암 경계선이 돌고, 궤도에서는 그 한 바퀴가 한 시간 반이다.
        /// </summary>
        private static void LocatePlanet(SkyView view, PlanetTile tile)
        {
            if (!view.InSpace) return;

            int hash = Gen.HashCombineInt(tile.Valid ? tile.tileId : 0, 0x2545F491);

            view.HasPlanet = true;
            view.PlanetAzimuth = (hash & 0xFFFF) / 65535f * SkyMath.TwoPi;
            view.PlanetRadiusRad = PlanetRadiusRad;

            // 소행성에서는 지평선 위로 얹고, 정거장에서는 갑판 아래에 둔다.
            view.PlanetAltitude = view.Place == SkyPlace.Asteroid ? 0.32f : -0.95f;

            float apart = SkyMath.SeparationAltAz(view.PlanetAltitude, view.PlanetAzimuth,
                                                  view.SunAltitude, view.SunAzimuth);

            view.PlanetLit = SkyMath.LitFraction(apart);
        }

        /// <summary>
        /// 달. 위성 궤도층이 등록된 세계에만 뜬다.
        ///
        /// 바닐라 오디세이는 그런 층을 싣지 않는다 - 정의 파일에 주석으로만 들어 있다.
        /// 그러니 여기는 대개 조용하고, 누군가 위성을 더하는 날 저절로 켜진다.
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
