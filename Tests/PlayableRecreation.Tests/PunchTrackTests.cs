using Punching.Core;
using Xunit;

namespace PlayableRecreation.Tests
{
    /// <summary>펀칭백의 악보와 채점 - 연타가 아니라 리듬이 정답이어야 한다.</summary>
    public class PunchTrackTests
    {
        [Fact]
        public void 같은_시드는_같은_악보를_낸다()
        {
            PunchTrack a = PunchTrack.Generate(4242, 2);
            PunchTrack b = PunchTrack.Generate(4242, 2);

            Assert.Equal(a.Notes.Count, b.Notes.Count);
            for (int i = 0; i < a.Notes.Count; i++)
                Assert.Equal(a.Notes[i].Beat, b.Notes[i].Beat, 6);
        }

        [Fact]
        public void 박은_시간순이고_마지막이_큰_한_방이다()
        {
            PunchTrack track = PunchTrack.Generate(7, 4);

            for (int i = 1; i < track.Notes.Count; i++)
                Assert.True(track.Notes[i].Beat > track.Notes[i - 1].Beat);

            Assert.True(track.Notes[track.Notes.Count - 1].Ko);
            for (int i = 0; i < track.Notes.Count - 1; i++)
                Assert.False(track.Notes[i].Ko);
        }

        [Fact]
        public void 난이도가_오르면_템포가_빨라진다()
        {
            Assert.True(PunchTrack.Generate(1, 4).BeatSeconds < PunchTrack.Generate(1, 0).BeatSeconds);
        }

        [Fact]
        public void 목표는_만점_안쪽이다()
        {
            for (int tier = 0; tier < 5; tier++)
            {
                PunchTrack track = PunchTrack.Generate(11, tier);
                Assert.InRange(track.TargetScore, 1, track.MaxScore);
            }
        }

        [Fact]
        public void 정박은_정타_살짝_어긋나면_스침이다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            Assert.Equal(NoteJudge.Perfect, track.RegisterHit(first, 0f));

            float second = track.Notes[1].Beat * track.BeatSeconds;
            Assert.Equal(NoteJudge.Good, track.RegisterHit(second + 0.15f, 0f));

            Assert.Equal(PunchTrack.PerfectScore + PunchTrack.GoodScore, track.Score);
            Assert.Equal(1, track.Perfects);
            Assert.Equal(1, track.Goods);
        }

        [Fact]
        public void 창_밖의_주먹은_헛스윙이고_콤보가_끊긴다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            track.RegisterHit(first, 0f);
            Assert.Equal(1, track.Combo);

            track.RegisterHit(first + 0.7f, 0f);

            Assert.Equal(1, track.Whiffs);
            Assert.Equal(0, track.Combo);
            Assert.Equal(1, track.MaxCombo);
            Assert.False(track.Flawless);
        }

        [Fact]
        public void 지나친_박은_미스로_굳는다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            track.AdvanceMisses(first + PunchTrack.GoodWindow + 0.1f);

            Assert.Equal(1, track.Misses);
            Assert.Equal(NoteJudge.Miss, track.Notes[0].Judge);
            Assert.False(track.Flawless);
        }

        [Fact]
        public void 큰_한_방은_세_배다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);

            // 앞의 박을 전부 정타로 치운 뒤 마지막 한 방.
            for (int i = 0; i < track.Notes.Count; i++)
                track.RegisterHit(track.Notes[i].Beat * track.BeatSeconds, 0f);

            Assert.True(track.LastHitKo);
            Assert.Equal(track.MaxScore, track.Score);
            Assert.True(track.Flawless);
        }

        [Fact]
        public void 프레임_보정은_창을_넓힌다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            // 창 바로 바깥이지만 보정만큼은 살려 준다.
            Assert.NotEqual(NoteJudge.Miss, track.RegisterHit(first + PunchTrack.GoodWindow + 0.03f, 0.05f));
        }

        /// <summary>
        /// 미스를 굳히는 잣대도 보정을 같이 받아야 한다. 좁게 굳히면 늦은 주먹이
        /// 닿기 전에 이미 미스가 되어, 넓혀 준 창이 늦은 쪽에서는 없는 것이 된다.
        /// </summary>
        [Fact]
        public void 보정_안의_늦은_박은_아직_미스가_아니다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            track.AdvanceMisses(first + PunchTrack.GoodWindow + 0.03f, 0.05f);

            Assert.Equal(0, track.Misses);
            Assert.Equal(NoteJudge.Pending, track.Notes[0].Judge);

            // 그래서 같은 순간에 도착한 주먹이 아직 이 박을 잡을 수 있다.
            Assert.NotEqual(NoteJudge.Miss, track.RegisterHit(first + PunchTrack.GoodWindow + 0.03f, 0.05f));
            Assert.Equal(0, track.Whiffs);
        }

        [Fact]
        public void 보정_밖의_늦은_박은_미스로_굳는다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);
            float first = track.Notes[0].Beat * track.BeatSeconds;

            track.AdvanceMisses(first + PunchTrack.GoodWindow + 0.08f, 0.05f);

            Assert.Equal(1, track.Misses);
            Assert.Equal(NoteJudge.Miss, track.Notes[0].Judge);
        }

        /// <summary>세트 끝을 한 프레임에 지나쳐도 안 친 발은 전부 미스로 남아야 한다.</summary>
        [Fact]
        public void 끝을_건너뛰어도_안_친_발은_미스로_남는다()
        {
            PunchTrack track = PunchTrack.Generate(3, 0);

            float past = track.LastNoteTime + 10f;
            Assert.True(track.Done(past));

            track.AdvanceMisses(past, 0f);

            Assert.Equal(track.Notes.Count, track.Misses);
            Assert.Equal(track.Notes.Count, track.JudgedCount);
            Assert.False(track.Flawless);
        }
    }
}
