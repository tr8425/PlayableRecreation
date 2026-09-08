using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Stargazing.Core;
using PlayableRecreation;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Stargazing
{
    /// <summary>
    /// 망원경. 이길 것도 질 것도 없는 첫 항목이다 - 프레임워크는 그런 것도 받는다.
    ///
    /// 별은 세계 시드가 정하므로 저장할 것이 없고, 어디에 찍힐지는 그 자리의
    /// 위도와 경도와 시각이 정한다. 중력선이 움직이면 타일이 바뀌고, 하늘도 따라 바뀐다.
    /// </summary>
    public class StargazingWorker : MiniGameWorker
    {
        private const float PanelWidth = 252f;
        private const float Gap = 12f;
        private const float RefreshInterval = 0.15f;

        /// <summary>
        /// 아래 땅을 다시 읽는 간격. 하늘은 눈에 띄게 흐르지만 땅은 그렇지 않다 -
        /// 명암 경계선이 한 칸 움직이는 데도 한참 걸리므로 자주 훑을 이유가 없다.
        /// </summary>
        private const float GroundInterval = 1.1f;

        /// <summary>여기까지 좁혀 봐야 지표의 이름표를 단다. 그보다 넓으면 점만 남는다.</summary>
        private const int NameRings = 5;

        /// <summary>처음부터 그어져 있는 별자리 수. 이름표가 그만큼 준비되어 있다.</summary>
        private const int KnownCount = 11;
        private const int NameCount = 14;

        private const float PickRadius = 13f;

        private struct Plot
        {
            public int Star;
            public Vector2 At;
            public float Size;
            public float Alpha;
        }

        /// <summary>망원경을 어느 쪽으로 돌려 두었는가. 아래를 보는 것은 궤도에서만 된다.</summary>
        private enum SkyMode { Stars, Ground }

        private StarField field;
        private readonly List<Constellation> known = new List<Constellation>();
        private SkyView view;
        private GroundPatch patch;
        private SkyMode mode;

        private Rect disc;
        private Vector2 center;
        private float radius;

        private readonly List<Plot> plots = new List<Plot>();
        private readonly Dictionary<int, Vector2> where = new Dictionary<int, Vector2>();

        private float nextRefresh;
        private float nextGround;
        private Rect lastDisc;

        /// <summary>아래를 볼 때 몇 겹까지 훑는가. 넓히면 칸이 작아지는 대신 멀리 본다.</summary>
        private int groundRings = GroundWatch.DefaultRings;

        private bool drawing;
        private readonly List<int> chain = new List<int>();
        private int hovered = -1;

        private Vector2 listScroll;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint { get { return 0; } }
        public override int Rounds { get { return 0; } }
        public override bool IsOver { get { return false; } }
        public override bool PlayerWon { get { return false; } }

        // ---------- 수명 ----------

        public override void StartNew(int seed)
        {
            int world = WorldSeed;

            field = new StarField(world);
            BuildKnown(world);

            drawing = false;
            chain.Clear();
            mode = SkyMode.Stars;
            nextRefresh = 0f;
        }

        public override void Resume(MiniGameSaveData data)
        {
            // 이어 볼 것이 없다. 하늘은 언제나 거기 있다.
            StartNew(0);
        }

        private static int WorldSeed
        {
            get
            {
                World world = Find.World;
                return world != null && world.info != null ? world.info.Seed : 0;
            }
        }

        /// <summary>
        /// 처음부터 그어져 있는 선들. 사람이 하늘을 보면 어차피 잇게 되어 있다는 쪽에 가깝다 -
        /// 모양도 이름도 세계 시드에서 나오므로, 세계마다 다르되 그 세계에서는 언제나 같다.
        /// </summary>
        private void BuildKnown(int seed)
        {
            known.Clear();

            List<int[]> shapes = ConstellationMaker.Generate(field, KnownCount, seed);

            // 이름표를 섞어 나눠 준다. 같은 이름이 늘 같은 모양에 붙지 않게.
            int[] order = new int[NameCount];
            for (int i = 0; i < NameCount; i++) order[i] = i;

            SkyRng rng = new SkyRng(seed ^ 0x1B873593);

            for (int i = NameCount - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            for (int i = 0; i < shapes.Count; i++)
                known.Add(new Constellation(shapes[i], "STG.Const." + order[i % NameCount], false));
        }

        // ---------- 진행 ----------

        public override void Tick(float now)
        {
            if (field == null) return;
            if (now < nextRefresh && disc == lastDisc) return;

            nextRefresh = now + RefreshInterval;
            lastDisc = disc;

            view = SkyWatch.Observe(Board);
            Rebuild();
        }

        private Map Here
        {
            get { return Board != null ? Board.Map : Find.CurrentMap; }
        }

        /// <summary>지금 이 순간 어느 별이 어디에 찍히는가. 매 프레임 다시 셀 필요는 없다.</summary>
        private void Rebuild()
        {
            plots.Clear();
            where.Clear();

            if (field == null || view == null || radius <= 1f) return;

            // 아래를 보는 중이면 별을 셀 일이 없다.
            if (mode == SkyMode.Ground && view.InSpace)
            {
                float now = Time.realtimeSinceStartup;

                if (patch == null || now >= nextGround)
                {
                    nextGround = now + GroundInterval;
                    patch = GroundWatch.Observe(Here, groundRings);
                }

                return;
            }

            patch = null;

            float limit = view.LimitMagnitude;
            float span = limit - StarField.BrightestMagnitude;
            if (span < 0.01f) span = 0.01f;

            for (int i = 0; i < field.Count; i++)
            {
                Star star = field.Stars[i];
                if (star.Magnitude > limit) continue;

                float altitude, azimuth;
                SkyMath.AltAz(star.Ra, star.Dec, view.LatitudeRad, view.Sidereal, out altitude, out azimuth);

                // 행성 뒤로 들어간 별은 가려진다. 하늘에서 가장 큰 것이 하는 일이다.
                if (Occulted(altitude, azimuth)) continue;

                Vector2 at;
                if (!ProjectSky(altitude, azimuth, out at)) continue;

                float bright = Mathf.Clamp01((limit - star.Magnitude) / span);

                plots.Add(new Plot
                {
                    Star = i,
                    At = at,
                    Size = Mathf.Lerp(1.7f, 7.6f, bright * bright),
                    Alpha = Mathf.Lerp(0.30f, 1f, bright),
                });

                where[i] = at;
            }
        }

        // ---------- 투영 ----------

        /// <summary>
        /// 이 자리에서 쓰는 투영. 발밑에 무언가 있으면 지평선 위만 담고,
        /// 갑판뿐인 정거장에서는 구 전체를 담는다. 이 판단이 필요한 곳은 전부 여기를 지난다.
        /// </summary>
        private bool ProjectSky(float altitude, float azimuth, out Vector2 at)
        {
            bool horizon = view.HasHorizon;
            if (horizon && altitude <= 0f) { at = Vector2.zero; return false; }

            float x, y;
            if (horizon) SkyMath.Project(altitude, azimuth, out x, out y);
            else SkyMath.ProjectFull(altitude, azimuth, out x, out y);

            at = center + new Vector2(x, y) * radius;
            return true;
        }

        /// <summary>각도 하나가 화면에서 몇 픽셀인가. 투영에 따라 눈금이 다르다.</summary>
        private float ScreenAngle(float radians)
        {
            float span = view.HasHorizon ? SkyMath.HalfPi : SkyMath.HalfPi * 2f;
            return radians / span * radius;
        }

        /// <summary>행성 뒤인가. 하늘에서 가장 큰 것은 그 뒤의 것을 가린다.</summary>
        private bool Occulted(float altitude, float azimuth)
        {
            if (!view.HasPlanet) return false;

            return SkyMath.SeparationAltAz(altitude, azimuth, view.PlanetAltitude, view.PlanetAzimuth)
                 < view.PlanetRadiusRad;
        }

        private static Rect Square(Vector2 at, float half)
        {
            return new Rect(at.x - half, at.y - half, half * 2f, half * 2f);
        }

        /// <summary>하늘의 바탕색. 그림자를 밀어 넣을 때 이 색으로 덮는다.</summary>
        private Color Backdrop
        {
            get
            {
                return view.InSpace
                    ? StarTheme.Void
                    : Color.Lerp(StarTheme.Sky, StarTheme.SkyDay, view.Glow);
            }
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            if (field == null) return;

            Rect panel = new Rect(area.xMax - PanelWidth, area.y, PanelWidth, area.height);
            Rect sky = new Rect(area.x, area.y, area.width - PanelWidth - Gap, area.height);

            LayoutDisc(sky);

            // Tick 은 판이 얼마나 큰지 알기 전에 돈다. 자리가 잡히거나 바뀐 첫 프레임은 여기서 세운다.
            if (view == null || disc != lastDisc)
            {
                if (view == null) view = SkyWatch.Observe(Board);

                lastDisc = disc;
                Rebuild();
            }

            // 지상에서는 아래를 볼 것이 없다. 발밑이 곧 아래다.
            if (!view.InSpace) Switch(SkyMode.Stars);

            if (mode == SkyMode.Ground) DrawGroundArea();
            else DrawSkyArea();

            DrawPanel(panel);
        }

        private void DrawSkyArea()
        {
            DrawDisc();
            TrackPointer();
            DrawPlanet();
            DrawKnown();
            DrawMine();
            DrawChain();
            DrawStars();
            DrawMoon();
            DrawObjects();
            DrawCompass();
            HandleClicks();
        }

        /// <summary>모드를 바꾼다. 별을 잇던 중이었다면 거기서 손을 뗀다.</summary>
        private void Switch(SkyMode next)
        {
            if (mode == next) return;

            mode = next;
            drawing = false;
            chain.Clear();
            hovered = -1;

            Rebuild();
            PRSounds.Play(StarSounds.Pick);
        }

        /// <summary>
        /// 배율을 바꾼다. 걸음이 양수면 넓게, 음수면 가깝게 - 바다 한가운데서
        /// 온통 같은 파랑만 보인다면 넓히는 쪽이 답이다.
        /// </summary>
        private void Zoom(int step)
        {
            int next = Mathf.Clamp(groundRings + step, GroundWatch.MinRings, GroundWatch.MaxRings);
            if (next == groundRings) return;

            groundRings = next;

            // 다음 훑기를 기다리지 않고 바로 다시 읽는다. 배율은 손이 돌린 것이라 즉시 답해야 한다.
            patch = null;
            Rebuild();
            PRSounds.Play(StarSounds.Pick);
        }

        private void LayoutDisc(Rect sky)
        {
            float size = Mathf.Max(80f, Mathf.Min(sky.width, sky.height));

            disc = new Rect(sky.center.x - size * 0.5f, sky.center.y - size * 0.5f, size, size);
            center = disc.center;
            radius = size * 0.5f - 14f;
        }

        private void DrawDisc()
        {
            // 낮에는 하늘이 밝다. 별이 사라진 것이 아니라 안 보이는 것이라는 표시다.
            GUI.color = Backdrop;
            GUI.DrawTexture(disc, PRTextures.Dot);

            GUI.color = StarTheme.Grid;
            for (int ring = 1; ring <= 2; ring++)
            {
                float part = radius * ring / 3f;
                GUI.DrawTexture(new Rect(center.x - part, center.y - part, part * 2f, part * 2f),
                    PRTextures.Outline);
            }

            GUI.color = StarTheme.Horizon;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                PRTextures.Outline);
            GUI.color = Color.white;
        }

        /// <summary>
        /// 방위. 궤도에서 하늘을 볼 때는 뜻이 없어 빼지만,
        /// 아래를 볼 때는 다시 뜻이 생긴다 - 지도의 위가 북쪽이다.
        /// </summary>
        private void DrawCompass()
        {
            if (view.InSpace && mode != SkyMode.Ground) return;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = StarTheme.Compass;

            Mark(new Vector2(center.x, center.y - radius - 8f), "STG.Compass.N".Translate());
            Mark(new Vector2(center.x, center.y + radius + 8f), "STG.Compass.S".Translate());
            Mark(new Vector2(center.x + radius + 10f, center.y), "STG.Compass.E".Translate());
            Mark(new Vector2(center.x - radius - 10f, center.y), "STG.Compass.W".Translate());

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void Mark(Vector2 at, string text)
        {
            Widgets.Label(new Rect(at.x - 20f, at.y - 10f, 40f, 20f), text);
        }

        private void DrawStars()
        {
            for (int i = 0; i < plots.Count; i++)
            {
                Plot plot = plots[i];
                float size = plot.Size;

                GUI.color = StarTheme.StarColor(field.Stars[plot.Star].Warmth, plot.Alpha);
                GUI.DrawTexture(new Rect(plot.At.x - size * 0.5f, plot.At.y - size * 0.5f, size, size),
                    PRTextures.Dot);
            }

            GUI.color = Color.white;

            if (StargazingSettings.ShowLabels) DrawStarLabels();
            if (hovered >= 0) DrawHover();
        }

        private void DrawStarLabels()
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.78f, 0.82f, 0.92f, 0.45f);

            for (int i = 0; i < plots.Count; i++)
            {
                Plot plot = plots[i];
                if (field.Stars[plot.Star].Magnitude > 0.6f) continue;

                Widgets.Label(new Rect(plot.At.x + 6f, plot.At.y - 9f, 70f, 18f),
                    field.Designation(plot.Star));
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawHover()
        {
            Vector2 at;
            if (!where.TryGetValue(hovered, out at)) return;

            GUI.color = StarTheme.Pick;
            GUI.DrawTexture(new Rect(at.x - 9f, at.y - 9f, 18f, 18f), PRTextures.Outline);
            GUI.color = Color.white;

            Star star = field.Stars[hovered];

            TooltipHandler.TipRegion(new Rect(at.x - 10f, at.y - 10f, 20f, 20f),
                "STG.Star.Tip".Translate(field.Designation(hovered), star.Magnitude.ToString("0.0")));
        }

        private void TrackPointer()
        {
            hovered = -1;
            if (Event.current.type == EventType.Layout) return;

            Vector2 mouse = Event.current.mousePosition;
            if (!disc.Contains(mouse)) return;

            float best = PickRadius * PickRadius;

            for (int i = 0; i < plots.Count; i++)
            {
                float gap = (plots[i].At - mouse).sqrMagnitude;
                if (gap >= best) continue;

                best = gap;
                hovered = plots[i].Star;
            }
        }

        /// <summary>
        /// 행성. 궤도에서 하늘을 보면 가장 큰 것은 별이 아니라 방금 떠나온 곳이다.
        ///
        /// 차고 기우는 것은 달과 같은 방법으로 만든다. 다른 것은 주기뿐이다 -
        /// 궤도에서는 해가 한 시간 반마다 뜨므로, 명암 경계선도 그만큼 빨리 돈다.
        /// </summary>
        private void DrawPlanet()
        {
            if (!view.HasPlanet) return;

            Vector2 at;
            if (!ProjectSky(view.PlanetAltitude, view.PlanetAzimuth, out at)) return;

            float size = ScreenAngle(view.PlanetRadiusRad);
            if (size < 4f) return;

            GUI.color = StarTheme.Planet;
            GUI.DrawTexture(Square(at, size), PRTextures.Dot);

            float shift = (1f - view.PlanetLit) * 2f * size;

            GUI.color = Backdrop;
            GUI.DrawTexture(Square(at + Nightward(at) * shift, size), PRTextures.Dot);

            // 어두운 쪽도 테두리는 남는다. 거기 있다는 것까지 지울 이유는 없다.
            GUI.color = StarTheme.PlanetRim;
            GUI.DrawTexture(Square(at, size), PRTextures.Outline);
            GUI.color = Color.white;

            TooltipHandler.TipRegion(Square(at, size),
                "STG.Planet.Tip".Translate(Mathf.RoundToInt(view.PlanetLit * 100f)));
        }

        /// <summary>해의 반대쪽. 그림자는 언제나 이쪽으로 밀린다.</summary>
        private Vector2 Nightward(Vector2 at)
        {
            float x, y;
            if (view.HasHorizon) SkyMath.Project(view.SunAltitude, view.SunAzimuth, out x, out y);
            else SkyMath.ProjectFull(view.SunAltitude, view.SunAzimuth, out x, out y);

            Vector2 away = at - (center + new Vector2(x, y) * radius);
            return away.sqrMagnitude < 1f ? Vector2.left : away.normalized;
        }

        /// <summary>
        /// 달. 차고 기우는 것은 그림자를 덧그려 만든다 -
        /// 밝은 원 위에 하늘색 원을 밀어 얹으면 그 경계가 그대로 명암 경계선이 된다.
        /// </summary>
        private void DrawMoon()
        {
            if (!view.HasMoon) return;

            float altitude, azimuth;
            SkyMath.AltAz(view.MoonRa, view.MoonDec, view.LatitudeRad, view.Sidereal,
                          out altitude, out azimuth);

            Vector2 at;
            if (!ProjectSky(altitude, azimuth, out at)) return;

            float size = Mathf.Max(11f, radius * 0.055f);

            GUI.color = StarTheme.Moon;
            GUI.DrawTexture(Square(at, size), PRTextures.Dot);

            // 초승달일수록 그림자를 더 많이 밀어 넣는다.
            float shift = (1f - view.MoonLit) * 2f * size;

            GUI.color = Backdrop;
            GUI.DrawTexture(Square(at + Vector2.left * shift, size), PRTextures.Dot);
            GUI.color = Color.white;

            TooltipHandler.TipRegion(Square(at, size),
                "STG.Moon.Tip".Translate(Mathf.RoundToInt(view.MoonLit * 100f)));
        }

        /// <summary>궤도 위의 것들. 오디세이가 없으면 목록이 비어 있으므로 여기는 아무것도 그리지 않는다.</summary>
        private void DrawObjects()
        {
            if (view.Objects.Count == 0) return;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            for (int i = 0; i < view.Objects.Count; i++)
            {
                SkyObject item = view.Objects[i];

                float altitude, azimuth;
                SkyMath.AltAz(item.Ra, item.Dec, view.LatitudeRad, view.Sidereal, out altitude, out azimuth);

                Vector2 at;
                if (!ProjectSky(altitude, azimuth, out at)) continue;
                if (Occulted(altitude, azimuth)) continue;

                Color ink = item.Craft ? StarTheme.Craft : StarTheme.Rock;

                GUI.color = ink;
                GUI.DrawTexture(new Rect(at.x - 5f, at.y - 5f, 10f, 10f), PRTextures.Outline);
                Widgets.Label(new Rect(at.x + 8f, at.y - 9f, 120f, 18f), item.Label);
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        // ---------- 아래 보기 ----------

        /// <summary>
        /// 망원경을 아래로 돌린다. 지어낸 지형이 아니라 이 세계가 가지고 있는 타일들이다 -
        /// 색은 그 타일의 기온·강수·고도에서 나오고, 어두운 칸은 그냥 그쪽이 밤인 것이다.
        /// 운이 좋으면 명암 경계선이 화면을 가로지르는 것을 본다.
        /// </summary>
        private void DrawGroundArea()
        {
            // 원반 위에서 굴린 휠은 배율이다. 옆 패널의 목록은 제 스크롤을 따로 가진다.
            if (Event.current.type == EventType.ScrollWheel && disc.Contains(Event.current.mousePosition))
            {
                Zoom(Event.current.delta.y > 0f ? 1 : -1);
                Event.current.Use();
            }

            GUI.color = StarTheme.Void;
            GUI.DrawTexture(disc, PRTextures.Dot);
            GUI.color = Color.white;

            if (patch == null || !patch.Valid)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = PRTheme.Dim;
                Widgets.Label(disc, "STG.Ground.Nothing".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float reach = radius * 0.92f;
            float half = Mathf.Max(4f, patch.Spacing * reach * 0.56f);

            for (int i = 0; i < patch.Cells.Count; i++)
            {
                GroundCell cell = patch.Cells[i];
                if (cell.At.magnitude > 1.04f) continue;

                Rect box = Square(center + cell.At * reach, half);

                GUI.color = StarTheme.GroundInk(cell);
                GUI.DrawTexture(box, PRTextures.Dot);
                GUI.color = Color.white;

                // 넓게 보면 이름표를 다 달 자리가 없다. 그래도 짚으면 무엇인지는 말해 준다.
                string tip = cell.Mark.NullOrEmpty() ? cell.Label
                    : cell.Label.NullOrEmpty() ? cell.Mark
                    : cell.Mark + "\n" + cell.Label;

                if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(box, tip);
            }

            DrawGroundMarks(reach, half);

            GUI.color = StarTheme.Horizon;
            GUI.DrawTexture(Square(center, radius), PRTextures.Outline);
            GUI.color = Color.white;

            DrawCompass();
        }

        /// <summary>
        /// 바로 아래에 과녁을 두고, 사람이 사는 곳에는 이름을 단다.
        /// 넓게 보는 중이면 이름은 접고 점만 남긴다 - 저기 무언가 있다는 것까지만 알려 주고,
        /// 궁금하면 가깝게 당겨 보라는 뜻이다.
        /// </summary>
        private void DrawGroundMarks(float reach, float half)
        {
            // 창 크기가 아니라 겹 수로 정한다. 큰 창에서는 칸이 커지지만, 넓게 볼수록
            // 이름표끼리 부딪히는 것은 마찬가지다.
            bool named = patch.Rings <= NameRings;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            for (int i = 0; i < patch.Cells.Count; i++)
            {
                GroundCell cell = patch.Cells[i];
                if (cell.At.magnitude > 1.04f) continue;

                Vector2 at = center + cell.At * reach;

                if (cell.Center)
                {
                    GUI.color = StarTheme.Pick;
                    GUI.DrawTexture(Square(at, half + 2.5f), PRTextures.Outline);
                    GUI.color = Color.white;
                }

                if (cell.Mark.NullOrEmpty()) continue;

                if (!cell.Lit)
                {
                    // 밤 쪽에 사람이 살면 불빛이 먼저 보인다.
                    GUI.color = StarTheme.NightHalo;
                    GUI.DrawTexture(Square(at, 8f), PRTextures.Dot);
                    GUI.color = StarTheme.NightLight;
                }
                else
                {
                    GUI.color = StarTheme.MineInk;
                }

                GUI.DrawTexture(Square(at, 3.5f), PRTextures.Dot);
                if (named) Widgets.Label(new Rect(at.x + 7f, at.y - 9f, 110f, 18f), cell.Mark);
                GUI.color = Color.white;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        // ---------- 별자리 ----------

        private void DrawKnown()
        {
            if (!StargazingSettings.ShowKnown) return;

            for (int i = 0; i < known.Count; i++)
                DrawShape(known[i].Stars, known[i].Name.Translate(), StarTheme.Known, StarTheme.KnownInk);
        }

        private void DrawMine()
        {
            StargazingComponent component = StargazingComponent.Current;
            if (component == null) return;

            List<PlayerConstellation> mine = component.Drawn;

            for (int i = 0; i < mine.Count; i++)
                DrawShape(mine[i].stars, mine[i].name, StarTheme.Mine, StarTheme.MineInk);
        }

        private void DrawShape(IList<int> stars, string name, Color line, Color ink)
        {
            Vector2 sum = Vector2.zero;
            Vector2 previous = Vector2.zero;
            bool hasPrevious = false;
            int seen = 0;

            for (int i = 0; i < stars.Count; i++)
            {
                Vector2 at;
                if (!where.TryGetValue(stars[i], out at)) { hasPrevious = false; continue; }

                if (hasPrevious) Widgets.DrawLine(previous, at, line, 1f);

                previous = at;
                hasPrevious = true;
                sum += at;
                seen++;
            }

            // 절반도 떠 있지 않으면 이름표를 달지 않는다. 지평선 밑에 이름만 뜨는 꼴이 된다.
            if (seen == 0 || seen * 2 < stars.Count) return;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ink;
            Widgets.Label(new Rect(sum.x / seen - 70f, sum.y / seen - 26f, 140f, 18f), name);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawChain()
        {
            if (!drawing || chain.Count == 0) return;

            Vector2 previous = Vector2.zero;
            bool hasPrevious = false;

            for (int i = 0; i < chain.Count; i++)
            {
                Vector2 at;
                if (!where.TryGetValue(chain[i], out at)) continue;

                if (hasPrevious) Widgets.DrawLine(previous, at, StarTheme.Drawing, 1.6f);

                previous = at;
                hasPrevious = true;

                GUI.color = StarTheme.Pick;
                GUI.DrawTexture(new Rect(at.x - 7f, at.y - 7f, 14f, 14f), PRTextures.Outline);
                GUI.color = Color.white;
            }

            if (!hasPrevious || Event.current.type == EventType.Layout) return;
            if (!disc.Contains(Event.current.mousePosition)) return;

            // 마지막으로 고른 별에서 마우스까지 미리 이어 보여준다.
            Color faint = StarTheme.Drawing;
            faint.a = 0.35f;
            Widgets.DrawLine(previous, Event.current.mousePosition, faint, 1f);
        }

        private void HandleClicks()
        {
            if (!Widgets.ButtonInvisible(disc)) return;
            if (!drawing || hovered < 0) return;

            // 방금 고른 별을 다시 누르면 취소된다.
            if (chain.Count > 0 && chain[chain.Count - 1] == hovered)
            {
                chain.RemoveAt(chain.Count - 1);
                PRSounds.Play(StarSounds.Cancel);
                return;
            }

            if (chain.Contains(hovered)) return;

            chain.Add(hovered);
            PRSounds.Play(StarSounds.Pick);
        }

        // ---------- 큰 버튼 ----------

        public override string ActionLabel
        {
            get
            {
                if (mode == SkyMode.Ground) return "STG.Btn.Sky".Translate().ToString();
                if (!drawing) return "STG.Btn.Draw".Translate().ToString();
                if (chain.Count < 3) return "STG.Btn.NeedMore".Translate(3 - chain.Count).ToString();

                return "STG.Btn.Name".Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get { return mode == SkyMode.Ground || !drawing || chain.Count >= 3; }
        }

        public override void DoAction()
        {
            if (mode == SkyMode.Ground) { Switch(SkyMode.Stars); return; }

            if (!drawing)
            {
                drawing = true;
                chain.Clear();
                return;
            }

            if (chain.Count < 3) return;

            List<int> picked = new List<int>(chain);

            Find.WindowStack.Add(new Dialog_NameConstellation(
                "STG.Name.Suggestion".Translate().ToString(),
                delegate (string name)
                {
                    StargazingComponent component = StargazingComponent.Current;
                    if (component != null) component.Add(name, picked);

                    PRSounds.Play(StarSounds.Name);
                    drawing = false;
                    chain.Clear();
                }));
        }

        // ---------- 옆 패널 ----------

        private void DrawPanel(Rect panel)
        {
            Widgets.DrawMenuSection(panel);
            Rect inner = panel.ContractedBy(10f);

            float y = inner.y;

            bool below = mode == SkyMode.Ground;

            // 고를 것은 궤도에 올라와야 생긴다. 지상에서는 아래를 볼 수 없다.
            if (view.InSpace)
            {
                DrawModes(new Rect(inner.x, y, inner.width, 26f));
                y += 34f;
            }

            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                below ? "STG.Panel.Below".Translate() : "STG.Panel.Where".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;

            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), below ? BelowText : PlaceText);
            y += 22f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), below ? BelowTimeText : TimeText);
            y += 26f;

            GUI.color = below || view.Clear ? PRTheme.ActiveTurn : PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), SeeingText);
            GUI.color = Color.white;
            y += 30f;

            if (below)
            {
                DrawZoomRow(new Rect(inner.x, y, inner.width, 24f));
                y += 32f;
            }

            if (drawing)
            {
                Rect cancel = new Rect(inner.x, y, inner.width, 28f);
                if (Widgets.ButtonText(cancel, "STG.Btn.Cancel".Translate()))
                {
                    drawing = false;
                    chain.Clear();
                    PRSounds.Play(StarSounds.Cancel);
                }

                y += 34f;
            }

            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), "STG.Panel.Mine".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;

            DrawMineList(new Rect(inner.x, y, inner.width, inner.yMax - y));
        }

        /// <summary>
        /// 배율 조절. 원반 위에서 휠을 굴려도 되지만, 버튼이 없으면 아무도 굴려 보지 않는다.
        /// 가운데에는 지금 가로로 몇 칸을 보고 있는지가 뜬다.
        /// </summary>
        private void DrawZoomRow(Rect row)
        {
            const float button = 28f;

            Rect wider = new Rect(row.x, row.y, button, row.height);
            Rect closer = new Rect(row.xMax - button, row.y, button, row.height);
            Rect span = new Rect(wider.xMax + 4f, row.y, closer.x - wider.xMax - 8f, row.height);

            TooltipHandler.TipRegion(wider, "STG.Ground.Wider".Translate());
            TooltipHandler.TipRegion(closer, "STG.Ground.Closer".Translate());

            bool canWiden = groundRings < GroundWatch.MaxRings;
            bool canClose = groundRings > GroundWatch.MinRings;

            if (Button(wider, "-", canWiden)) Zoom(1);
            if (Button(closer, "+", canClose)) Zoom(-1);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = PRTheme.Dim;
            Widgets.Label(span, "STG.Ground.Span".Translate(patch != null ? patch.Across : 0));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static bool Button(Rect rect, string label, bool enabled)
        {
            if (enabled) return Widgets.ButtonText(rect, label);

            GUI.color = PRTheme.Dim;
            Widgets.ButtonText(rect, label, true, false, false);
            GUI.color = Color.white;
            return false;
        }

        /// <summary>하늘이냐 지표냐. 보고 있는 쪽에는 표시가 남는다.</summary>
        private void DrawModes(Rect row)
        {
            float half = (row.width - 4f) * 0.5f;

            Rect sky = new Rect(row.x, row.y, half, row.height);
            Rect ground = new Rect(row.xMax - half, row.y, half, row.height);

            if (Widgets.ButtonText(sky, "STG.Mode.Sky".Translate())) Switch(SkyMode.Stars);
            if (Widgets.ButtonText(ground, "STG.Mode.Ground".Translate())) Switch(SkyMode.Ground);

            Widgets.DrawHighlightSelected(mode == SkyMode.Stars ? sky : ground);
        }

        private void DrawMineList(Rect area)
        {
            StargazingComponent component = StargazingComponent.Current;
            List<PlayerConstellation> mine = component != null ? component.Drawn : new List<PlayerConstellation>();

            if (mine.Count == 0)
            {
                GUI.color = PRTheme.Dim;
                Text.Font = GameFont.Tiny;
                Widgets.Label(area, "STG.Panel.None".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                return;
            }

            const float rowHeight = 26f;
            Rect content = new Rect(0f, 0f, area.width - 18f, mine.Count * rowHeight + 4f);

            Widgets.BeginScrollView(area, ref listScroll, content);

            PlayerConstellation remove = null;

            for (int i = 0; i < mine.Count; i++)
            {
                Rect row = new Rect(0f, i * rowHeight, content.width, rowHeight);
                Rect kill = new Rect(row.xMax - 24f, row.y + 2f, 22f, 22f);

                GUI.color = StarTheme.MineInk;
                Widgets.Label(new Rect(row.x + 2f, row.y, row.width - 30f, rowHeight), mine[i].name);
                GUI.color = Color.white;

                TooltipHandler.TipRegion(kill, "STG.Panel.Erase".Translate());
                if (Widgets.ButtonText(kill, "x")) remove = mine[i];
            }

            Widgets.EndScrollView();

            if (remove == null || component == null) return;

            component.Remove(remove);
            PRSounds.Play(StarSounds.Cancel);
        }

        // ---------- 문자열 ----------

        private string PlaceText
        {
            get
            {
                float latitude = view.LatitudeRad / SkyMath.Deg2Rad;

                string north = latitude >= 0f ? "STG.Dir.North".Translate() : "STG.Dir.South".Translate();
                string east = view.LongitudeDeg >= 0f ? "STG.Dir.East".Translate() : "STG.Dir.West".Translate();

                // 궤도 좌표는 행성 위의 자리가 아니라 그 상공의 자리다. 표시도 그렇게 한다.
                string key = view.InSpace ? "STG.Panel.CoordsOrbit" : "STG.Panel.Coords";

                return key.Translate(
                    Mathf.Abs(latitude).ToString("0.0"), north,
                    Mathf.Abs(view.LongitudeDeg).ToString("0.0"), east);
            }
        }

        private string BelowText
        {
            get
            {
                if (patch == null || !patch.Valid) return "STG.Ground.Nothing".Translate().ToString();
                if (patch.BelowRegion.NullOrEmpty()) return patch.BelowLabel ?? string.Empty;

                return "STG.Panel.BelowNamed".Translate(patch.BelowLabel, patch.BelowRegion).ToString();
            }
        }

        private string BelowTimeText
        {
            get
            {
                if (patch == null || !patch.Valid) return string.Empty;

                return "STG.Panel.BelowTime".Translate(
                    Mathf.FloorToInt(patch.BelowHour),
                    (patch.BelowLit ? "STG.Ground.Lit" : "STG.Ground.Dark").Translate()).ToString();
            }
        }

        private string TimeText
        {
            get
            {
                return "STG.Panel.Time".Translate(
                    view.Season.LabelCap(), view.DayOfYear + 1, Mathf.FloorToInt(view.Hour));
            }
        }

        private string SeeingText
        {
            get
            {
                if (mode == SkyMode.Ground)
                    return "STG.Seeing.Ground".Translate(patch != null ? patch.Cells.Count : 0).ToString();

                if (view.Place == SkyPlace.Asteroid)
                    return "STG.Seeing.Asteroid".Translate(plots.Count).ToString();

                if (view.Place == SkyPlace.Station)
                    return "STG.Seeing.Station".Translate(plots.Count).ToString();

                if (view.Glow > 0.55f) return "STG.Seeing.Day".Translate(plots.Count).ToString();
                if (!view.Clear) return "STG.Seeing.Poor".Translate(view.WeatherLabel, plots.Count).ToString();

                return "STG.Seeing.Clear".Translate(plots.Count).ToString();
            }
        }

        public override string StatusText
        {
            get
            {
                if (view == null) return string.Empty;

                if (mode == SkyMode.Ground) return "STG.Status.Ground".Translate().ToString();

                if (drawing)
                    return chain.Count < 3
                        ? "STG.Status.Drawing".Translate(chain.Count).ToString()
                        : "STG.Status.DrawingReady".Translate(chain.Count).ToString();

                if (hovered >= 0)
                    return "STG.Status.Star".Translate(
                        field.Designation(hovered), field.Stars[hovered].Magnitude.ToString("0.0")).ToString();

                if (view.HasPlanet)
                    return "STG.Status.Planet".Translate(
                        Mathf.RoundToInt(view.PlanetLit * 100f)).ToString();

                if (view.HasMoon)
                    return "STG.Status.WithMoon".Translate(
                        Mathf.RoundToInt(view.MoonLit * 100f), view.Objects.Count).ToString();

                if (view.Objects.Count > 0)
                    return "STG.Status.WithObjects".Translate(view.Objects.Count).ToString();

                return "STG.Status.Idle".Translate().ToString();
            }
        }

        public override void DoSettings(Listing_Standard list)
        {
            StargazingSettings.DoSettings(list);
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (field == null) StartNew(0);
            if (view == null) view = SkyWatch.Observe(null);

            LayoutDisc(area.ContractedBy(8f));
            Rebuild();

            DrawDisc();
            DrawPlanet();

            if (page >= 1) DrawKnown();
            DrawStars();
            if (page != 2) DrawCompass();
        }
    }
}
