using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 모드가 들고 있는 그림을 이름으로 찾아 준다.
    ///
    /// 정적 필드에 <see cref="ContentFinder{T}"/> 결과를 한 번 담아 두는 흔한 방식은
    /// 조용히 실패한다 - 그림이 그 사이 파괴되면 필드는 널이 되고, 화면에서는 아무 일도
    /// 없었다는 듯 그 자리만 비고, 로그에는 프레임마다 같은 줄이 쌓인다.
    /// 그래서 여기서는 그릴 때마다 살아 있는지 보고, 죽었으면 다시 굽는다.
    ///
    /// 찾는 순서는 ContentFinder 먼저, 안 되면 모드 폴더에서 직접 읽기다.
    /// 뒤쪽은 앞쪽이 어떤 이유로 못 찾든 상관없이 파일만 있으면 통한다.
    /// </summary>
    public static class PRContent
    {
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> givenUp = new HashSet<string>();

        /// <summary><paramref name="path"/> 는 Textures 아래 경로에서 확장자를 뺀 것. 예: "PR/Chess/pawn".</summary>
        public static Texture2D Texture(string path)
        {
            Texture2D texture;

            // 유니티에서 파괴된 개체는 널과 같다고 나온다. 캐시에 있어도 다시 봐야 하는 이유다.
            if (cache.TryGetValue(path, out texture) && texture != null) return texture;

            // 한 번 없다고 판명된 것은 다시 찾지 않는다. 프레임마다 디스크를 두드릴 일이 아니다.
            if (givenUp.Contains(path)) return null;

            texture = ContentFinder<Texture2D>.Get(path, false);
            if (texture == null) texture = FromFile(path);

            cache[path] = texture;

            if (texture == null)
            {
                givenUp.Add(path);
                Log.Warning("[Playable Recreation] 그림을 찾지 못했습니다: Textures/" + path
                            + ".png (모드 폴더: " + (RootDir ?? "알 수 없음") + ")");
            }

            return texture;
        }

        private static Texture2D FromFile(string path)
        {
            string root = RootDir;
            if (root.NullOrEmpty()) return null;

            string file = Path.Combine(root, Path.Combine("Textures", path.Replace('/', Path.DirectorySeparatorChar) + ".png"));
            if (!File.Exists(file)) return null;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, true);
            if (!texture.LoadImage(File.ReadAllBytes(file)))
            {
                Object.Destroy(texture);
                return null;
            }

            texture.name = path;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(true);

            return texture;
        }

        private static string RootDir
        {
            get
            {
                PRMod mod = LoadedModManager.GetMod<PRMod>();
                return mod != null && mod.Content != null ? mod.Content.RootDir : null;
            }
        }
    }
}
