using PlayableRecreation;
using PlayableRecreation.Core;
using PlayableRecreation.UI;
using UnityEngine;
using Verse;

namespace Slots
{
    /// <summary>
    /// 슬롯머신. 별 보기처럼 승부가 아니다 (hasMatch=false) - 의사결정이 없는 게임에
    /// 난이도와 전적을 붙이는 것은 거짓말이라서다. 세션 칩 스무 닢으로 어디까지 가는지,
    /// 그것이 전부다. 진짜 은화는 한 닢도 걸리지 않는다 - 도박 경제는 카지노 모드의 몫이다.
    ///
    /// 릴은 슬롯머신의 그 얼굴들이다 - 7, 코인 자리엔 금, BAR, 체리 자리엔 딸기,
    /// 레몬 자리엔 이 행성답게 해골. 7과 BAR 는 굽고, 나머지는 바닐라 아이콘을 빌려 쓴다.
    ///
    /// 릴은 (시드, 순번)으로 결정된다. 당기는 순간 결과는 이미 정해져 있다 -
    /// 원래 슬롯머신이 그런 물건이다.
    /// </summary>
    public class SlotsGameWorker : MiniGameWorker
    {
        private const int StartCredits = 20;
        private const int Symbols = 5;          // 7 · 금 · BAR · 딸기 · 해골
        private const int SymbolSeven = 0;
        private const int SymbolBar = 2;
        private const float ReelStop0 = 0.55f;
        private const float ReelStopGap = 0.4f;
        private const float JackpotSeconds = 3.2f;

        private static readonly int[] Pay3 = { 150, 50, 20, 10, 5 };
        private const int PayPairSeven = 5;

        /// <summary>릴에 도는 물건들. 7(0)과 BAR(2)는 def 가 아니라 구운 그림이다.</summary>
        private static ThingDef[] symbolDefs;

        private static ThingDef[] SymbolDefs
        {
            get
            {
                if (symbolDefs == null)
                {
                    symbolDefs = new[]
                    {
                        null,
                        DefDatabase<ThingDef>.GetNamedSilentFail("Gold"),
                        null,
                        DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Strawberry"),
                        DefDatabase<ThingDef>.GetNamedSilentFail("Skull"),
                    };
                }

                return symbolDefs;
            }
        }

        private int seed;
        private int spinIndex;

        private int credits = StartCredits;
        private int peak = StartCredits;
        private int lastWin;
        private int spins;

        private readonly int[] reels = new int[3];
        private bool spinning;
        private float spinStart;
        private float now;
        private float lastNow;
        private int stopped;
        private readonly float[] stopAt = new float[3];

        /// <summary>칩 표시는 실제 값을 천천히 따라간다 - 잭팟은 한 닢씩 세면서 올라야 잭팟이다.</summary>
        private float shownCredits = StartCredits;

        private float winFlashUntil;
        private float jackpotUntil;
        private float nextDing;
        private int dingsLeft;

        // ---------- 프레임워크에 답하는 것들 ----------

        public override int SavePoint
        {
            get { return spins; }
        }

        public override int Rounds
        {
            get { return spins; }
        }

        /// <summary>승부가 아니니 끝도 없다. 창을 닫는 순간이 끝이다.</summary>
        public override bool IsOver
        {
            get { return false; }
        }

        public override bool PlayerWon
        {
            get { return false; }
        }

        public override string StatusText
        {
            get
            {
                if (credits <= 0 && !spinning) return "SLT.Status.Broke".Translate(peak).ToString();
                if (spinning) return "SLT.Status.Spinning".Translate().ToString();
                if (lastWin > 0) return "SLT.Status.Won".Translate(lastWin).ToString();
                return "SLT.Status.Idle".Translate().ToString();
            }
        }

        // ---------- 수명 ----------

        public override void StartNew(int newSeed)
        {
            seed = newSeed;
            spinIndex = 0;
            credits = StartCredits;
            shownCredits = StartCredits;
            peak = StartCredits;
            lastWin = 0;
            spins = 0;
            spinning = false;
            stopped = 0;
            winFlashUntil = 0f;
            jackpotUntil = 0f;
            dingsLeft = 0;
        }

        public override void Resume(MiniGameSaveData data)
        {
            StartNew(Rand.Int);
        }

        // ---------- 진행 ----------

        public override void Tick(float timeNow)
        {
            now = timeNow;
            float delta = lastNow <= 0f ? 0f : Mathf.Min(0.1f, now - lastNow);
            lastNow = now;

            // 칩 표시가 실제 값을 쫓아간다. 딴 것이 클수록 빨리, 그래도 한동안은 센다.
            float gap = Mathf.Abs(credits - shownCredits);
            if (gap > 0.001f)
                shownCredits = Mathf.MoveTowards(shownCredits, credits, delta * Mathf.Max(6f, gap * 2.2f));

            // 잭팟의 종소리는 몇 번에 나눠 울린다.
            if (dingsLeft > 0 && now >= nextDing)
            {
                PRSounds.Play(SlotsSounds.Win);
                dingsLeft--;
                nextDing = now + 0.28f;
            }

            if (!spinning) return;

            float elapsed = now - spinStart;
            int shouldStop = Mathf.Clamp((int)((elapsed - ReelStop0) / ReelStopGap) + 1, 0, 3);

            while (stopped < shouldStop)
            {
                stopAt[stopped] = now;
                stopped++;
                PRSounds.Play(SlotsSounds.Stop);
            }

            if (stopped >= 3)
            {
                spinning = false;
                Settle();
            }
        }

        private int SymbolAt(int spin, int reel)
        {
            // 가중치 - 해골 30 · 딸기 25 · BAR 20 · 금 15 · 7 은 10. 좋은 것일수록 드물다.
            float u = AimMath.Uniform(seed, spin * 3 + reel);
            if (u < 0.10f) return 0;
            if (u < 0.25f) return 1;
            if (u < 0.45f) return 2;
            if (u < 0.70f) return 3;
            return 4;
        }

        private void Spin()
        {
            if (spinning || credits <= 0) return;

            credits--;
            lastWin = 0;
            spins++;

            for (int reel = 0; reel < 3; reel++) reels[reel] = SymbolAt(spinIndex, reel);
            spinIndex++;

            spinning = true;
            stopped = 0;
            spinStart = now;
            PRSounds.Play(SlotsSounds.Pull);
        }

        private void Settle()
        {
            int win = 0;

            if (reels[0] == reels[1] && reels[1] == reels[2]) win = Pay3[reels[0]];
            else
            {
                int sevens = 0;
                for (int i = 0; i < 3; i++) if (reels[i] == SymbolSeven) sevens++;
                if (sevens == 2) win = PayPairSeven;
            }

            if (win <= 0) return;

            credits += win;
            lastWin = win;
            winFlashUntil = now + 1.1f;
            if (credits > peak) peak = credits;

            if (win >= Pay3[0])
            {
                // 잭팟. 기계가 아는 가장 화려한 3초.
                jackpotUntil = now + JackpotSeconds;
                dingsLeft = 8;
                nextDing = now + 0.2f;
                PRSounds.Play(SlotsSounds.Jackpot);
            }
            else PRSounds.Play(SlotsSounds.Win);
        }

        // ---------- 조작 ----------

        public override void HandleShortcuts()
        {
            if (!PRKeys.ActionPressed()) return;
            if (!ActionEnabled) return;

            Spin();
            Event.current.Use();
        }

        public override string ActionLabel
        {
            get { return "SLT.Btn.Pull".Translate().ToString(); }
        }

        public override bool ActionEnabled
        {
            get { return !spinning && credits > 0; }
        }

        public override void DoAction()
        {
            if (ActionEnabled) Spin();
        }

        // ---------- 그리기 ----------

        public override void DrawPlayArea(Rect area)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = SlotsTheme.Credits;
            Widgets.Label(new Rect(area.x, area.y, area.width / 2f, 40f),
                "SLT.Credits".Translate(Mathf.FloorToInt(shownCredits)).ToString());

            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = PRTheme.Dim;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(area.center.x, area.y, area.width / 2f, 40f),
                "SLT.Peak".Translate(peak).ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect machine = new Rect(area.x + area.width * 0.1f, area.y + 52f,
                                    area.width * 0.8f, area.height - 64f);
            DrawMachine(machine);
        }

        private void DrawMachine(Rect area)
        {
            float windowHeight = Mathf.Min(120f, area.height * 0.5f);
            Rect cabinet = new Rect(area.x, area.y, area.width,
                                    Mathf.Min(area.height, windowHeight + 60f));

            bool jackpot = now < jackpotUntil;

            Widgets.DrawBoxSolid(cabinet, SlotsTheme.Cabinet);

            // 잭팟이면 캐비닛 테두리가 금색으로 번쩍인다.
            if (jackpot)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 10f);
                Color edge = Color.Lerp(SlotsTheme.WinFlash, SlotsTheme.JackpotEdge, pulse);
                Widgets.DrawBoxSolid(new Rect(cabinet.x, cabinet.y, cabinet.width, 4f), edge);
                Widgets.DrawBoxSolid(new Rect(cabinet.x, cabinet.yMax - 4f, cabinet.width, 4f), edge);
                Widgets.DrawBoxSolid(new Rect(cabinet.x, cabinet.y, 4f, cabinet.height), edge);
                Widgets.DrawBoxSolid(new Rect(cabinet.xMax - 4f, cabinet.y, 4f, cabinet.height), edge);
            }

            float slotWidth = (cabinet.width - 4f * 16f) / 3f;

            for (int reel = 0; reel < 3; reel++)
            {
                Rect window = new Rect(cabinet.x + 16f + reel * (slotWidth + 16f),
                                       cabinet.y + 30f, slotWidth, windowHeight);
                Widgets.DrawBoxSolid(window.ExpandedBy(2f), SlotsTheme.WindowEdge);
                Widgets.DrawBoxSolid(window, SlotsTheme.WindowBack);

                DrawReel(window, reel);
            }

            if (now < winFlashUntil && !spinning && !jackpot)
                Widgets.DrawBoxSolid(cabinet, SlotsTheme.WinFlash);

            // 딴 만큼이 캐비닛 위로 떠오른다.
            if (lastWin > 0 && now < winFlashUntil && !spinning)
            {
                float t = 1f - (winFlashUntil - now) / 1.1f;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(SlotsTheme.Credits.r, SlotsTheme.Credits.g, SlotsTheme.Credits.b, 1f - t * 0.7f);
                Widgets.Label(new Rect(cabinet.x, cabinet.y - 34f - t * 10f, cabinet.width, 30f), "+" + lastWin);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (jackpot) DrawJackpot(cabinet);
        }

        /// <summary>릴 하나. 도는 동안은 물건들이 실제로 흘러내려가고, 멈추면 살짝 튄다.</summary>
        private void DrawReel(Rect window, int reel)
        {
            float icon = Mathf.Min(window.width, window.height) * 0.62f;

            if (spinning && reel >= stopped)
            {
                // 흘러내려가는 릴. 결과는 이미 정해져 있고, 이것은 그냥 구경거리다.
                float speed = 9f + reel * 2.5f;
                float position = (now - spinStart) * speed;
                int index = Mathf.FloorToInt(position);
                float fraction = position - index;

                Widgets.BeginGroup(window);
                DrawScrollSymbol(window, index % Symbols, fraction * window.height, icon);
                DrawScrollSymbol(window, (index + 1) % Symbols, fraction * window.height - window.height, icon);
                Widgets.EndGroup();
                return;
            }

            // 멈춘 직후 0.15초는 살짝 주저앉았다 돌아온다.
            float bounce = 0f;
            if (reel < stopped)
            {
                float since = now - stopAt[reel];
                if (since < 0.15f) bounce = Mathf.Sin(since / 0.15f * Mathf.PI) * 6f;
            }

            Rect target = new Rect(window.center.x - icon / 2f,
                                   window.center.y - icon / 2f + bounce, icon, icon);
            DrawSymbol(target, reels[reel]);
        }

        /// <summary>그룹 좌표계 안에서 릴 창의 한 칸. 창 밖으로 나가는 부분은 잘린다.</summary>
        private static void DrawScrollSymbol(Rect window, int symbol, float yOffset, float icon)
        {
            Rect target = new Rect(window.width / 2f - icon / 2f,
                                   window.height / 2f - icon / 2f + yOffset, icon, icon);
            DrawSymbol(target, symbol);
        }

        private static void DrawSymbol(Rect rect, int symbol)
        {
            if (symbol == SymbolSeven)
            {
                GUI.DrawTexture(rect, SlotsTextures.Seven);
                return;
            }

            if (symbol == SymbolBar)
            {
                DrawBarPlate(rect);
                return;
            }

            ThingDef def = SymbolDefs[symbol];

            if (def != null)
            {
                Widgets.ThingIcon(rect, def);
                return;
            }

            // 어떤 이유로 바닐라 def 가 없다면 - 그림 없는 판보다는 동그라미가 낫다.
            GUI.color = SlotsTheme.Credits;
            GUI.DrawTexture(rect, PRTextures.Dot);
            GUI.color = Color.white;
        }

        /// <summary>슬롯머신의 그 BAR. 크림색 판에 굵은 검은 글자.</summary>
        private static void DrawBarPlate(Rect rect)
        {
            Rect plate = new Rect(rect.x, rect.center.y - rect.height * 0.26f,
                                  rect.width, rect.height * 0.52f);

            Widgets.DrawBoxSolid(plate.ExpandedBy(2f), SlotsTheme.WindowEdge);
            Widgets.DrawBoxSolid(plate, SlotsTheme.BarPlate);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = SlotsTheme.BarText;
            Widgets.Label(plate, "BAR");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>잭팟 - 7이 쏟아지고 글자가 고동친다.</summary>
        private void DrawJackpot(Rect cabinet)
        {
            float remain = jackpotUntil - now;

            // 쏟아지는 7. 열마다 자리와 박자가 달라 비처럼 보인다.
            Rect rain = new Rect(cabinet.x, cabinet.y, cabinet.width, cabinet.height + 46f);
            for (int i = 0; i < 22; i++)
            {
                float u = AimMath.Uniform(spinIndex * 31, i);
                float v = AimMath.Uniform(spinIndex * 31, i + 100);

                float x = rain.x + u * (rain.width - 18f);
                float fall = ((now * (0.55f + v * 0.5f) + v * 3f) % 1f);
                float y = rain.y - 10f + fall * rain.height;

                float size = 14f + v * 10f;
                GUI.DrawTexture(new Rect(x, y, size, size), SlotsTextures.Seven);
            }

            // 고동치는 글자. 마지막 0.6초에 잦아든다.
            float pulse = 0.75f + 0.25f * Mathf.Sin(now * 12f);
            float fade = Mathf.Clamp01(remain / 0.6f);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(SlotsTheme.JackpotEdge.r * pulse, SlotsTheme.JackpotEdge.g * pulse,
                                  SlotsTheme.JackpotEdge.b * pulse, fade);
            Widgets.Label(new Rect(cabinet.x, cabinet.yMax + 8f, cabinet.width, 34f),
                "SLT.Jackpot".Translate().ToString());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (page == 0)
            {
                // 릴 세 개를 그대로 보여준다 - 전부 7로.
                Rect machine = new Rect(area.x + area.width * 0.15f, area.center.y - 80f,
                                        area.width * 0.7f, 160f);
                int[] saved = { reels[0], reels[1], reels[2] };
                reels[0] = 0; reels[1] = 0; reels[2] = 0;
                bool wasSpinning = spinning;
                spinning = false;
                DrawMachine(machine);
                spinning = wasSpinning;
                reels[0] = saved[0]; reels[1] = saved[1]; reels[2] = saved[2];
                return;
            }

            Listing_Standard list = new Listing_Standard();
            list.Begin(new Rect(area.x + area.width * 0.16f, area.y + 24f, area.width * 0.68f, area.height - 24f));

            // 이름을 def 에서 가져오면 번역도 아이콘도 저절로 맞는다.
            list.Label("SLT.Tut.Pay.Seven".Translate(Pay3[0]));
            list.Label("SLT.Tut.Pay.Row".Translate(LabelOf(1), Pay3[1]));
            list.Label("SLT.Tut.Pay.Bar".Translate(Pay3[2]));
            list.Label("SLT.Tut.Pay.Row".Translate(LabelOf(3), Pay3[3]));
            list.Label("SLT.Tut.Pay.Row".Translate(LabelOf(4), Pay3[4]));
            list.Label("SLT.Tut.Pay.PairSeven".Translate(PayPairSeven));

            list.End();
        }

        private static string LabelOf(int symbol)
        {
            ThingDef def = SymbolDefs[symbol];
            return def != null ? def.LabelCap.ToString() : "?";
        }
    }
}
