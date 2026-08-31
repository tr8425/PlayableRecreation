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

        private StarField field;
        private readonly List<Constellation> known = new List<Constellation>();
        private SkyView view;

        private Rect disc;
        private Vector2 center;
        private float radius;

        private readonly List<Plot> plots = new List<Plot>();
        private readonly Dictionary<int, Vector2> where = new Dictionary<int, Vector2>();

        private float nextRefresh;
        private Rect lastDisc;

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

        /// <summary>지금 이 순간 어느 별이 어디에 찍히는가. 매 프레임 다시 셀 필요는 없다.</summary>
        private void Rebuild()
        {
            plots.Clear();
            where.Clear();

            if (field == null || view == null || radius <= 1f) return;

            float limit = view.LimitMagnitude;
            float span = limit - StarField.BrightestMagnitude;
            if (span < 0.01f) span = 0.01f;

            for (int i = 0; i < field.Count; i++)
            {
                Star star = field.Stars[i];
                if (star.Magnitude > limit) continue;

                float altitude, azimuth;
                SkyMath.AltAz(star.Ra, star.Dec, view.LatitudeRad, view.Sidereal, out altitude, out azimuth);

                if (!view.InSpace && altitude <= 0f) continue;

                float x, y;
                if (view.InSpace) SkyMath.ProjectFull(altitude, azimuth, out x, out y);
                else SkyMath.Project(altitude, azimuth, out x, out y);

                Vector2 at = center + new Vector2(x, y) * radius;
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

            DrawDisc();
            TrackPointer();
            DrawKnown();
            DrawMine();
            DrawChain();
            DrawStars();
            DrawMoon();
            DrawObjects();
            DrawCompass();
            HandleClicks();
            DrawPanel(panel);
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
            Color ground = view.InSpace
                ? new Color(0.02f, 0.02f, 0.035f)
                : Color.Lerp(StarTheme.Sky, StarTheme.SkyDay, view.Glow);

            GUI.color = ground;
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

        private void DrawCompass()
        {
            if (view.InSpace) return;

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
        /// 달. 차고 기우는 것은 그림자를 덧그려 만든다 -
        /// 밝은 원 위에 하늘색 원을 밀어 얹으면 그 경계가 그대로 명암 경계선이 된다.
        /// </summary>
        private void DrawMoon()
        {
            if (!view.HasMoon) return;

            float altitude, azimuth;
            SkyMath.AltAz(view.MoonRa, view.MoonDec, view.LatitudeRad, view.Sidereal,
                          out altitude, out azimuth);

            if (!view.InSpace && altitude <= 0f) return;

            float x, y;
            if (view.InSpace) SkyMath.ProjectFull(altitude, azimuth, out x, out y);
            else SkyMath.Project(altitude, azimuth, out x, out y);

            Vector2 at = center + new Vector2(x, y) * radius;
            float size = Mathf.Max(11f, radius * 0.055f);

            GUI.color = StarTheme.Moon;
            GUI.DrawTexture(new Rect(at.x - size, at.y - size, size * 2f, size * 2f), PRTextures.Dot);

            // 초승달일수록 그림자를 더 많이 밀어 넣는다.
            float shift = (1f - view.MoonLit) * 2f * size;

            GUI.color = view.InSpace
                ? new Color(0.02f, 0.02f, 0.035f)
                : Color.Lerp(StarTheme.Sky, StarTheme.SkyDay, view.Glow);

            GUI.DrawTexture(new Rect(at.x - size - shift, at.y - size, size * 2f, size * 2f), PRTextures.Dot);
            GUI.color = Color.white;

            TooltipHandler.TipRegion(new Rect(at.x - size, at.y - size, size * 2f, size * 2f),
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

                if (!view.InSpace && altitude <= 0f) continue;

                float x, y;
                if (view.InSpace) SkyMath.ProjectFull(altitude, azimuth, out x, out y);
                else SkyMath.Project(altitude, azimuth, out x, out y);

                Vector2 at = center + new Vector2(x, y) * radius;
                Color ink = item.Craft ? StarTheme.Craft : StarTheme.Rock;

                GUI.color = ink;
                GUI.DrawTexture(new Rect(at.x - 5f, at.y - 5f, 10f, 10f), PRTextures.Outline);
                Widgets.Label(new Rect(at.x + 8f, at.y - 9f, 120f, 18f), item.Label);
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
                if (!drawing) return "STG.Btn.Draw".Translate().ToString();
                if (chain.Count < 3) return "STG.Btn.NeedMore".Translate(3 - chain.Count).ToString();

                return "STG.Btn.Name".Translate().ToString();
            }
        }

        public override bool ActionEnabled
        {
            get { return !drawing || chain.Count >= 3; }
        }

        public override void DoAction()
        {
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

            Text.Font = GameFont.Tiny;
            GUI.color = PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f), "STG.Panel.Where".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 20f;

            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), PlaceText);
            y += 22f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), TimeText);
            y += 26f;

            GUI.color = view.Clear ? PRTheme.ActiveTurn : PRTheme.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), SeeingText);
            GUI.color = Color.white;
            y += 30f;

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

                return "STG.Panel.Coords".Translate(
                    Mathf.Abs(latitude).ToString("0.0"), north,
                    Mathf.Abs(view.LongitudeDeg).ToString("0.0"), east);
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
                if (view.InSpace) return "STG.Seeing.Space".Translate(plots.Count).ToString();
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

                if (drawing)
                    return chain.Count < 3
                        ? "STG.Status.Drawing".Translate(chain.Count).ToString()
                        : "STG.Status.DrawingReady".Translate(chain.Count).ToString();

                if (hovered >= 0)
                    return "STG.Status.Star".Translate(
                        field.Designation(hovered), field.Stars[hovered].Magnitude.ToString("0.0")).ToString();

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

            if (page >= 1) DrawKnown();
            DrawStars();
            if (page != 2) DrawCompass();
        }
    }
}
