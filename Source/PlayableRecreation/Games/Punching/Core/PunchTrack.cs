using System;
using System.Collections.Generic;
using PlayableRecreation.Core;

namespace Punching.Core
{
    public enum NoteJudge
    {
        Pending,
        Perfect,
        Good,
        Miss,
    }

    /// <summary>쳐야 할 한 박.</summary>
    public sealed class PunchNote
    {
        /// <summary>시작부터 몇 박째인가. 반 박도 있다 - 연타 구간이다.</summary>
        public float Beat;

        /// <summary>마지막 큰 한 방. 점수가 세 배다.</summary>
        public bool Ko;

        public NoteJudge Judge = NoteJudge.Pending;
    }

    /// <summary>
    /// 펀칭백 한 세션의 악보와 채점. Verse 를 모른다.
    ///
    /// 문법은 콜 앤 리스폰스다 - 백이 두 박 흔들리며 부르면(콜), 정박에 주먹으로 답한다.
    /// 패턴은 세 가지뿐이고, 난이도는 패턴을 바꾸는 대신 템포와 배합을 조인다.
    /// 연타가 아니라 리듬이 정답이 되도록 채점은 전부 박자와의 거리다.
    ///
    /// 악보는 (시드, 순번)에서 결정론으로 나온다 - 다시 열어도 같은 악보다.
    /// </summary>
    public sealed class PunchTrack
    {
        public const int PerfectScore = 100;
        public const int GoodScore = 50;
        public const int KoMultiplier = 3;

        /// <summary>판정 창(초). 템포가 올라도 창은 그대로다 - 그것이 난이도다.</summary>
        public const float PerfectWindow = 0.07f;
        public const float GoodWindow = 0.16f;

        /// <summary>첫 콜까지의 예비 박.</summary>
        public const float LeadInBeats = 2f;

        private readonly List<PunchNote> notes = new List<PunchNote>();
        private readonly List<float> cues = new List<float>();

        public float BeatSeconds { get; private set; }
        public int MaxScore { get; private set; }
        public int TargetScore { get; private set; }

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Perfects { get; private set; }
        public int Goods { get; private set; }
        public int Misses { get; private set; }
        public int Whiffs { get; private set; }

        /// <summary>판정이 끝난 발 수. 진행 표시용.</summary>
        public int JudgedCount { get; private set; }

        /// <summary>방금 맞힌 것이 마지막 큰 한 방이었나. 연출이 묻는다.</summary>
        public bool LastHitKo { get; private set; }

        public IReadOnlyList<PunchNote> Notes
        {
            get { return notes; }
        }

        /// <summary>콜 박(백이 흔들리는 박)들. 소리와 흔들림 연출이 여기에 맞춘다.</summary>
        public IReadOnlyList<float> Cues
        {
            get { return cues; }
        }

        public float LastNoteTime
        {
            get { return notes.Count == 0 ? 0f : notes[notes.Count - 1].Beat * BeatSeconds; }
        }

        public bool Done(float songTime)
        {
            return songTime > LastNoteTime + 1.2f;
        }

        // ---------- 악보 ----------

        private static float Bpm(int tier)
        {
            switch (tier)
            {
                case 0: return 66f;
                case 1: return 80f;
                case 2: return 94f;
                case 3: return 110f;
                default: return 126f;
            }
        }

        /// <summary>위로 갈수록 세트도 길어진다 - 템포만이 아니라 지구력도 시험이다.</summary>
        private static int PatternCount(int tier)
        {
            return 12 + tier * 2;
        }

        /// <summary>패턴 배합. 위로 갈수록 연타가 늘어난다.</summary>
        private static int PatternFor(int tier, float u)
        {
            float single, dbl;
            switch (tier)
            {
                case 0: single = 0.70f; dbl = 0.30f; break;
                case 1: single = 0.50f; dbl = 0.40f; break;
                case 2: single = 0.30f; dbl = 0.40f; break;
                case 3: single = 0.18f; dbl = 0.37f; break;
                default: single = 0.08f; dbl = 0.32f; break;
            }

            return u < single ? 0 : u < single + dbl ? 1 : 2;
        }

        private static float TargetShare(int tier)
        {
            switch (tier)
            {
                case 0: return 0.55f;
                case 1: return 0.64f;
                case 2: return 0.72f;
                case 3: return 0.80f;
                default: return 0.88f;
            }
        }

        public static PunchTrack Generate(int seed, int tier)
        {
            PunchTrack track = new PunchTrack { BeatSeconds = 60f / Bpm(tier) };

            float beat = LeadInBeats;

            for (int i = 0; i < PatternCount(tier); i++)
            {
                int pattern = PatternFor(tier, AimMath.Uniform(seed, i));

                track.cues.Add(beat);
                track.cues.Add(beat + 1f);

                switch (pattern)
                {
                    case 0:
                        track.notes.Add(new PunchNote { Beat = beat + 2f });
                        beat += 3f;
                        break;

                    case 1:
                        track.notes.Add(new PunchNote { Beat = beat + 2f });
                        track.notes.Add(new PunchNote { Beat = beat + 3f });
                        beat += 4f;
                        break;

                    default:
                        track.notes.Add(new PunchNote { Beat = beat + 2f });
                        track.notes.Add(new PunchNote { Beat = beat + 2.5f });
                        track.notes.Add(new PunchNote { Beat = beat + 3f });
                        track.notes.Add(new PunchNote { Beat = beat + 3.5f });
                        beat += 5f;
                        break;
                }
            }

            // 마지막 큰 한 방.
            track.cues.Add(beat);
            track.cues.Add(beat + 1f);
            track.notes.Add(new PunchNote { Beat = beat + 2f, Ko = true });

            track.MaxScore = (track.notes.Count - 1) * PerfectScore + PerfectScore * KoMultiplier;
            track.TargetScore = (int)Math.Round(track.MaxScore * TargetShare(tier) / 10.0) * 10;

            return track;
        }

        // ---------- 채점 ----------

        /// <summary>
        /// 주먹 하나를 판정한다. slack 은 프레임이 늘어질 때 창을 넓혀 주는 보정이다 -
        /// 입력이 프레임에 묶이는 만큼은 플레이어의 잘못이 아니다.
        /// </summary>
        public NoteJudge RegisterHit(float songTime, float slack)
        {
            int best = -1;
            float bestDistance = GoodWindow + slack;

            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].Judge != NoteJudge.Pending) continue;

                float distance = Math.Abs(songTime - notes[i].Beat * BeatSeconds);
                if (distance <= bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }

                // 아직 창에 들어오지도 않은 미래의 발이면 더 볼 것이 없다.
                if (notes[i].Beat * BeatSeconds > songTime + bestDistance) break;
            }

            if (best < 0)
            {
                Whiffs++;
                Combo = 0;
                return NoteJudge.Miss;
            }

            PunchNote note = notes[best];
            bool perfect = bestDistance <= PerfectWindow + slack * 0.5f;

            note.Judge = perfect ? NoteJudge.Perfect : NoteJudge.Good;
            JudgedCount++;
            LastHitKo = note.Ko;

            int gained = perfect ? PerfectScore : GoodScore;
            if (note.Ko) gained *= KoMultiplier;
            Score += gained;

            if (perfect) Perfects++;
            else Goods++;

            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;

            return note.Judge;
        }

        /// <summary>
        /// 창을 지나쳐 버린 발들을 미스로 굳힌다.
        ///
        /// slack 은 <see cref="RegisterHit"/> 이 받아 주는 것과 같은 값이어야 한다.
        /// 좁게 굳히면 늦은 주먹이 도착하기 전에 이미 미스로 확정되어, 프레임 보정이
        /// 늦은 쪽에서는 아예 작동하지 않고 같은 입력이 미스와 헛스윙으로 두 번 세어진다.
        /// </summary>
        public void AdvanceMisses(float songTime, float slack = 0f)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                PunchNote note = notes[i];
                if (note.Judge != NoteJudge.Pending) continue;

                if (note.Beat * BeatSeconds < songTime - GoodWindow - slack)
                {
                    note.Judge = NoteJudge.Miss;
                    JudgedCount++;
                    Misses++;
                    Combo = 0;
                }
                else break;
            }
        }

        public bool Flawless
        {
            get { return Misses == 0 && Whiffs == 0; }
        }
    }
}
