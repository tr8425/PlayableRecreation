using System.Collections.Generic;
using PlayableRecreation;
using PlayableRecreation.Core;
using PlayableRecreation.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace Slots
{
    /// <summary>
    /// 슬롯머신. 별 보기처럼 승부가 아니다 (hasMatch=false) - 의사결정이 없는 게임에
    /// 난이도와 전적을 붙이는 것은 거짓말이라서다.
    ///
    /// 칩은 식민지의 것이다 - 기계가 지갑을 기억하고(세이브에 저장), 은 200닢으로
    /// 스무 닢을 충전한다. 나가는 길은 없다 - 은을 도로 뱉는 순간 이 창은 오락이
    /// 아니라 환전소가 된다. 천 닢을 쌓으면 기계가 그것을 기억해 준다.
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

        private const int ChargeCost = 200;
        private const int ChargeTokens = 20;
        private const int ClubThreshold = 1000;

        /// <summary>세이브에 남는 지갑. 번역 키와 헷갈리지 않게 빗금 꼴을 쓴다.</summary>
        private const string KeyInit = "Slots/Init";
        private const string KeyTokens = "Slots/Tokens";
        private const string KeyPeak = "Slots/Peak";
        private const string KeyClub = "Slots/Club";

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

        private int lastWin;
        private int spins;

        /// <summary>이번 줄의 배당. 당기는 순간 확정되고, 릴이 멈출 때 드러난다.</summary>
        private int pendingWin;

        /// <summary>세이브가 없을 때(테스트 같은 비정상 상황)만 쓰는 예비 지갑.</summary>
        private int detachedTokens = StartCredits;

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

        /// <summary>천 닢 클럽 연출 중이면 잭팟 현수막 대신 클럽 현수막을 건다.</summary>
        private bool clubBanner;

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
                if (Credits <= 0 && !spinning) return "SLT.Status.Broke".Translate(Peak).ToString();
                if (spinning) return "SLT.Status.Spinning".Translate().ToString();
                if (lastWin > 0) return "SLT.Status.Won".Translate(lastWin).ToString();
                return "SLT.Status.Idle".Translate().ToString();
            }
        }

        // ---------- 지갑 ----------

        /// <summary>
        /// 지갑은 언제나 세이브가 들고 있다. 창이 이 값을 복사해 들고 있으면 안 된다 -
        /// 기계 두 대를 같이 열어 두면 나중에 쓰는 창이 앞선 창의 칩을 덮어써 버린다.
        /// 그래서 읽기도 쓰기도 매번 세이브를 거친다.
        /// </summary>
        private static GameComponent_Recreation Store
        {
            get { return GameComponent_Recreation.Current; }
        }

        private int Credits
        {
            get
            {
                GameComponent_Recreation store = Store;
                return store != null ? store.GetCounter(KeyTokens, StartCredits) : detachedTokens;
            }
        }

        private int Peak
        {
            get
            {
                GameComponent_Recreation store = Store;
                return store != null ? store.GetCounter(KeyPeak, StartCredits) : detachedTokens;
            }
        }

        /// <summary>칩을 더하거나 뺀다. 읽고-고치고-쓰기라 다른 창의 결과를 지우지 않는다.</summary>
        private void AddTokens(int delta)
        {
            GameComponent_Recreation store = Store;

            if (store == null)
            {
                detachedTokens = Mathf.Max(0, detachedTokens + delta);
                return;
            }

            int tokens = Mathf.Max(0, store.GetCounter(KeyTokens, StartCredits) + delta);
            store.SetCounter(KeyTokens, tokens);

            if (tokens > store.GetCounter(KeyPeak, StartCredits)) store.SetCounter(KeyPeak, tokens);
        }

        // ---------- 수명 ----------

        public override void StartNew(int newSeed)
        {
            seed = newSeed;
            spinIndex = 0;
            lastWin = 0;
            pendingWin = 0;
            spins = 0;
            spinning = false;
            stopped = 0;
            winFlashUntil = 0f;
            jackpotUntil = 0f;
            dingsLeft = 0;
            clubBanner = false;

            // 지갑은 세이브의 것이다. 처음 앉는 식민지에게만 하우스가 스무 닢을 내준다.
            GameComponent_Recreation store = Store;
            if (store != null && store.GetCounter(KeyInit) == 0)
            {
                store.SetCounter(KeyInit, 1);
                store.SetCounter(KeyTokens, StartCredits);
                store.SetCounter(KeyPeak, StartCredits);
            }

            shownCredits = Credits;
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
            // 도는 동안은 배당을 뺀 값을 보여준다 - 칩은 이미 지갑에 들어와 있지만,
            // 결과를 릴보다 먼저 알려주면 릴을 볼 이유가 없어진다.
            float target = spinning ? Credits - pendingWin : Credits;
            float gap = Mathf.Abs(target - shownCredits);
            if (gap > 0.001f)
                shownCredits = Mathf.MoveTowards(shownCredits, target, delta * Mathf.Max(6f, gap * 2.2f));

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
                Reveal();
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

        /// <summary>
        /// 당긴다. 판돈과 배당이 같은 순간에 오간다 - 릴이 도는 중에 창이 닫히거나
        /// 습격이 판을 끊어도 칩 한 닢만 나가고 딴 것은 사라지는 일이 없다.
        /// </summary>
        private void Spin()
        {
            if (spinning || Credits <= 0) return;

            for (int reel = 0; reel < 3; reel++) reels[reel] = SymbolAt(spinIndex, reel);
            spinIndex++;

            pendingWin = WinForReels();
            lastWin = 0;
            spins++;

            AddTokens(pendingWin - 1);

            spinning = true;
            stopped = 0;
            spinStart = now;
            PRSounds.Play(SlotsSounds.Pull);
        }

        /// <summary>지금 줄의 배당. 같은 그림 셋, 아니면 7 두 개의 위로금.</summary>
        private int WinForReels()
        {
            if (reels[0] == reels[1] && reels[1] == reels[2]) return Pay3[reels[0]];

            int sevens = 0;
            for (int i = 0; i < 3; i++) if (reels[i] == SymbolSeven) sevens++;
            return sevens == 2 ? PayPairSeven : 0;
        }

        /// <summary>릴이 다 멈췄다. 칩은 이미 오갔고, 여기서는 그것을 드러내기만 한다.</summary>
        private void Reveal()
        {
            if (pendingWin <= 0) return;

            lastWin = pendingWin;
            winFlashUntil = now + 1.1f;

            if (pendingWin >= Pay3[0])
            {
                // 잭팟. 기계가 아는 가장 화려한 3초.
                jackpotUntil = now + JackpotSeconds;
                clubBanner = false;
                dingsLeft = 8;
                nextDing = now + 0.2f;
                PRSounds.Play(SlotsSounds.Jackpot);
            }
            else PRSounds.Play(SlotsSounds.Win);

            CheckThousandClub();
        }

        // ---------- 충전 ----------

        /// <summary>
        /// 은 200닢이 칩 스무 닢이 된다. 식민지의 은만 센다 - 바닐라 거래가 쓰는 것과
        /// 같은 잣대다. 안개 속, 금지된 것, 그리고 거주 구역도 창고도 아닌 곳에 널린 것은
        /// 우리 것이 아니다. 그러지 않으면 이 버튼이 고대 위험 안의 은까지 원격으로 먹는다.
        /// </summary>
        private List<Thing> ColonySilver(Map map)
        {
            List<Thing> silver = new List<Thing>();

            List<Thing> all = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            for (int i = 0; i < all.Count; i++)
            {
                Thing thing = all[i];
                if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                if (thing.Position.Fogged(map)) continue;
                if (thing.IsForbidden(Faction.OfPlayer)) continue;
                if (!map.areaManager.Home[thing.Position] && !thing.IsInAnyStorage()) continue;

                silver.Add(thing);
            }

            return silver;
        }

        private void Charge()
        {
            if (spinning) return;

            Map map = Board != null ? Board.Map : null;
            if (map == null) return;

            // 파괴하면 lister 목록이 그 자리에서 줄어드니 복사본을 밟고 간다.
            List<Thing> silver = ColonySilver(map);

            int total = 0;
            for (int i = 0; i < silver.Count; i++) total += silver[i].stackCount;

            if (total < ChargeCost)
            {
                Messages.Message("SLT.Msg.NoSilver".Translate(ChargeCost),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            int remaining = ChargeCost;
            for (int i = 0; i < silver.Count && remaining > 0; i++)
            {
                Thing stack = silver[i];
                if (stack.Destroyed) continue;

                int take = Mathf.Min(stack.stackCount, remaining);
                stack.SplitOff(take).Destroy();
                remaining -= take;
            }

            AddTokens(ChargeTokens);
            PRSounds.Play(SlotsSounds.Win);
            CheckThousandClub();
        }

        /// <summary>
        /// 칩 천 닢. 은으로 바꿔 주는 길은 없다 - 그 순간 이 창은 오락이 아니라
        /// 환전소가 된다. 대신 기계가 이 일을 딱 한 번, 성대하게 기억해 준다.
        /// </summary>
        private void CheckThousandClub()
        {
            if (Credits < ClubThreshold) return;

            GameComponent_Recreation store = Store;
            if (store == null || store.GetCounter(KeyClub) != 0) return;
            store.SetCounter(KeyClub, 1);

            jackpotUntil = now + JackpotSeconds;
            clubBanner = true;
            dingsLeft = 8;
            nextDing = now + 0.2f;
            PRSounds.Play(SlotsSounds.Jackpot);

            if (SeatedPawn != null)
            {
                Messages.Message("SLT.Msg.Club".Translate(SeatedPawn.LabelShortCap),
                    SeatedPawn, MessageTypeDefOf.PositiveEvent, false);

                ThoughtDef club = DefDatabase<ThoughtDef>.GetNamedSilentFail("PR_SlotsThousandClub");
                if (club != null && SeatedPawn.needs != null && SeatedPawn.needs.mood != null)
                    SeatedPawn.needs.mood.thoughts.memories.TryGainMemory(club);
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
            get { return !spinning && Credits > 0; }
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
                "SLT.Peak".Translate(Peak).ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect machine = new Rect(area.x + area.width * 0.1f, area.y + 52f,
                                    area.width * 0.8f, area.height - 64f);
            DrawMachine(machine);
            DrawChargeButton(area);
        }

        /// <summary>은을 칩으로 바꾸는 작은 버튼. 릴이 도는 동안은 잠긴다.</summary>
        private void DrawChargeButton(Rect area)
        {
            const float width = 220f;
            const float height = 30f;
            Rect button = new Rect(area.center.x - width / 2f, area.yMax - height - 2f, width, height);

            TooltipHandler.TipRegion(button, "SLT.Btn.Charge.Tip".Translate(ChargeCost, ChargeTokens));

            string label = "SLT.Btn.Charge".Translate(ChargeTokens, ChargeCost).ToString();

            if (spinning)
            {
                GUI.color = PRTheme.Dim;
                Widgets.ButtonText(button, label, true, false, false);
                GUI.color = Color.white;
                return;
            }

            if (Widgets.ButtonText(button, label)) Charge();
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
                (clubBanner ? "SLT.Club" : "SLT.Jackpot").Translate().ToString());
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
