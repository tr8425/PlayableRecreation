using PlayableRecreation;
using PlayableRecreation.UI;
using PlayableRecreation.Core;
using UnityEngine;
using Verse;

namespace Slots
{
    /// <summary>
    /// 슬롯머신. 별 보기처럼 승부가 아니다 (hasMatch=false) - 의사결정이 없는 게임에
    /// 난이도와 전적을 붙이는 것은 거짓말이라서다. 세션 칩 스무 닢으로 어디까지 가는지,
    /// 그것이 전부다. 진짜 은화는 한 닢도 걸리지 않는다 - 도박 경제는 카지노 모드의 몫이다.
    ///
    /// 릴은 (시드, 순번)으로 결정된다. 당기는 순간 결과는 이미 정해져 있다 -
    /// 원래 슬롯머신이 그런 물건이다.
    /// </summary>
    public class SlotsGameWorker : MiniGameWorker
    {
        private const int StartCredits = 20;
        private const int Symbols = 4;          // 7 · BAR · 동전 · 자두
        private const float ReelStop0 = 0.55f;
        private const float ReelStopGap = 0.4f;

        private static readonly int[] Pay3 = { 100, 25, 10, 5 };
        private const int PayPairSevens = 2;

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
        private int stopped;

        private float winFlashUntil;

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
            peak = StartCredits;
            lastWin = 0;
            spins = 0;
            spinning = false;
            stopped = 0;
        }

        public override void Resume(MiniGameSaveData data)
        {
            StartNew(Rand.Int);
        }

        // ---------- 진행 ----------

        public override void Tick(float timeNow)
        {
            now = timeNow;
            if (!spinning) return;

            float elapsed = now - spinStart;
            int shouldStop = Mathf.Clamp((int)((elapsed - ReelStop0) / ReelStopGap) + 1, 0, 3);

            while (stopped < shouldStop)
            {
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
            // 가중치 - 자두 4 · 동전 3 · BAR 2 · 7 하나. 좋은 것일수록 드물다.
            float u = AimMath.Uniform(seed, spin * 3 + reel);
            if (u < 0.1f) return 0;
            if (u < 0.3f) return 1;
            if (u < 0.6f) return 2;
            return 3;
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
                for (int i = 0; i < 3; i++) if (reels[i] == 0) sevens++;
                if (sevens == 2) win = PayPairSevens;
            }

            if (win > 0)
            {
                credits += win;
                lastWin = win;
                winFlashUntil = now + 0.8f;
                if (credits > peak) peak = credits;

                PRSounds.Play(win >= Pay3[0] ? SlotsSounds.Jackpot : SlotsSounds.Win);
            }
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
                "SLT.Credits".Translate(credits).ToString());

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
            Widgets.DrawBoxSolid(cabinet, SlotsTheme.Cabinet);

            float slotWidth = (cabinet.width - 4f * 16f) / 3f;

            for (int reel = 0; reel < 3; reel++)
            {
                Rect window = new Rect(cabinet.x + 16f + reel * (slotWidth + 16f),
                                       cabinet.y + 30f, slotWidth, windowHeight);
                Widgets.DrawBoxSolid(window.ExpandedBy(2f), SlotsTheme.WindowEdge);
                Widgets.DrawBoxSolid(window, SlotsTheme.WindowBack);

                int symbol;
                if (spinning && reel >= stopped)
                {
                    // 도는 릴은 그냥 빠르게 넘어가는 그림이다. 결과는 이미 정해져 있다.
                    symbol = (int)((now - spinStart) * (14f + reel * 3f)) % Symbols;
                }
                else symbol = reels[reel];

                DrawSymbol(window, symbol);
            }

            if (now < winFlashUntil && !spinning)
                Widgets.DrawBoxSolid(cabinet, SlotsTheme.WinFlash);
        }

        private static void DrawSymbol(Rect window, int symbol)
        {
            Vector2 center = window.center;

            switch (symbol)
            {
                case 0:
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = SlotsTheme.Seven;
                    Widgets.Label(window, "7");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;

                case 1:
                    GUI.color = SlotsTheme.Bar;
                    for (int i = -1; i <= 1; i++)
                        Widgets.DrawBoxSolid(new Rect(center.x - window.width * 0.28f,
                            center.y + i * 12f - 4f, window.width * 0.56f, 8f), SlotsTheme.Bar);
                    GUI.color = Color.white;
                    break;

                case 2:
                    GUI.color = SlotsTheme.Coin;
                    GUI.DrawTexture(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), PRTextures.Dot);
                    GUI.color = Color.white;
                    break;

                default:
                    GUI.color = SlotsTheme.Plum;
                    GUI.DrawTexture(new Rect(center.x - 16f, center.y - 16f, 32f, 32f), PRTextures.Dot);
                    GUI.color = SlotsTheme.Cabinet;
                    Widgets.DrawBoxSolid(new Rect(center.x - 1f, center.y - 24f, 2f, 10f), SlotsTheme.Plum);
                    GUI.color = Color.white;
                    break;
            }
        }

        // ---------- 튜토리얼 도식 ----------

        public override void DrawTutorialFigure(Rect area, int page)
        {
            if (page == 0)
            {
                // 릴 세 개를 그대로 보여준다.
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

            list.Label("SLT.Tut.Pay.Sevens".Translate(Pay3[0]));
            list.Label("SLT.Tut.Pay.Bars".Translate(Pay3[1]));
            list.Label("SLT.Tut.Pay.Coins".Translate(Pay3[2]));
            list.Label("SLT.Tut.Pay.Plums".Translate(Pay3[3]));
            list.Label("SLT.Tut.Pay.Pair".Translate(PayPairSevens));

            list.End();
        }
    }
}
