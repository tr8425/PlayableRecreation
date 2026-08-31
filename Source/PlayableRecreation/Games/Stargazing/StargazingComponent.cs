using System.Collections.Generic;
using Verse;

namespace Stargazing
{
    /// <summary>플레이어가 직접 이어 이름 붙인 별자리 하나.</summary>
    public class PlayerConstellation : IExposable
    {
        public string name = "?";
        public List<int> stars = new List<int>();

        public PlayerConstellation()
        {
        }

        public PlayerConstellation(string name, List<int> stars)
        {
            this.name = name;
            this.stars = new List<int>(stars);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name", "?");
            Scribe_Collections.Look(ref stars, "stars", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && stars == null) stars = new List<int>();
        }
    }

    /// <summary>
    /// 하늘은 세계의 것이지 망원경의 것이 아니다. 그래서 별자리는 가구가 아니라 여기에 남는다 -
    /// 망원경을 부수고 새로 지어도, 다른 정착지로 옮겨 가도 이어 둔 선은 그대로다.
    ///
    /// 별 자체는 저장하지 않는다. 세계 시드만 있으면 언제든 똑같이 다시 만들어진다.
    /// </summary>
    public class StargazingComponent : GameComponent
    {
        private List<PlayerConstellation> drawn = new List<PlayerConstellation>();

        public StargazingComponent(Game game)
        {
        }

        public static StargazingComponent Current
        {
            get
            {
                Game game = Verse.Current.Game;
                return game != null ? game.GetComponent<StargazingComponent>() : null;
            }
        }

        public List<PlayerConstellation> Drawn
        {
            get { return drawn ?? (drawn = new List<PlayerConstellation>()); }
        }

        public void Add(string name, List<int> stars)
        {
            if (stars == null || stars.Count < 3) return;

            Drawn.Add(new PlayerConstellation(name.NullOrEmpty() ? "?" : name.Trim(), stars));
        }

        public void Remove(PlayerConstellation constellation)
        {
            if (constellation != null) Drawn.Remove(constellation);
        }

        /// <summary>그 별이 이미 어느 별자리에 들어가 있는가.</summary>
        public bool Uses(int star)
        {
            for (int i = 0; i < Drawn.Count; i++)
                if (Drawn[i].stars.Contains(star)) return true;

            return false;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref drawn, "drawnConstellations", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && drawn == null)
                drawn = new List<PlayerConstellation>();
        }
    }
}
