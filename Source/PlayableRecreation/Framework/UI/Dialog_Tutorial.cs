using System;
using UnityEngine;
using Verse;

namespace PlayableRecreation.UI
{
    /// <summary>
    /// 규칙 안내. 쪽 넘김과 문구는 프레임워크가, 매 쪽의 그림은 게임이 그린다.
    /// 처음 판을 열 때 자동으로 한 번, 이후에는 창의 ? 버튼이나 설정에서 다시 볼 수 있다.
    /// </summary>
    public class Dialog_Tutorial : Window
    {
        private const float TitleHeight = 38f;
        private const float FigureHeight = 250f;
        private const float FooterHeight = 40f;
        private const float DotSize = 9f;
        private const float DotGap = 7f;
        private const float Pad = 12f;

        private static readonly Color DotIdle = new Color(1f, 1f, 1f, 0.13f);

        private readonly MiniGameDef game;
        private readonly MiniGameWorker figures;
        private readonly Action onFinished;
        private int page;

        public override Vector2 InitialSize
        {
            get { return new Vector2(680f, 600f); }
        }

        private int PageCount
        {
            get { return Mathf.Max(1, game.tutorialPages); }
        }

        /// <param name="onFinished">끝까지 보거나 건너뛴 뒤 실행할 동작. 없으면 그냥 닫힌다.</param>
        public Dialog_Tutorial(MiniGameDef game, Action onFinished)
        {
            this.game = game;
            this.onFinished = onFinished;
            figures = game.WorkerSample;

            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            // 읽는 창이다. 규칙을 읽거나 전적을 보는 동안 콜로니가 굴러가서는 안 된다.
            forcePause = Current.ProgramState == ProgramState.Playing;
        }

        /// <summary>규칙 안내는 게임마다 따로 센다. 우르를 읽었다고 편자를 건너뛰지 않는다.</summary>
        public static bool SeenFor(MiniGameDef game)
        {
            return game != null && PRMod.Settings.GetBool(game.defName + ".tutorialSeen", false);
        }

        public override void PostClose()
        {
            base.PostClose();

            PRMod.Settings.SetBool(game.defName + ".tutorialSeen", true);
            PRMod.Settings.Write();

            if (onFinished != null) onFinished();
        }

        public override void DoWindowContents(Rect inRect)
        {
            HandleKeyboard();

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, TitleHeight), Key("Title"));
            Text.Font = GameFont.Small;

            Rect figure = new Rect(inRect.x, inRect.y + TitleHeight, inRect.width, FigureHeight);
            if (figures != null) figures.DrawTutorialFigure(figure, page);

            Rect body = new Rect(inRect.x, figure.yMax + Pad, inRect.width,
                                 inRect.height - TitleHeight - FigureHeight - FooterHeight - Pad * 2f);
            Widgets.Label(body, Key("Body"));

            DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight));
        }

        private string Key(string suffix)
        {
            return (game.tutorialKeyPrefix + ".P" + (page + 1) + "." + suffix).Translate().ToString();
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            if (Event.current.keyCode == KeyCode.RightArrow) { Advance(1); Event.current.Use(); }
            else if (Event.current.keyCode == KeyCode.LeftArrow) { Advance(-1); Event.current.Use(); }
        }

        private void DrawFooter(Rect footer)
        {
            const float buttonWidth = 120f;

            if (page > 0 && Widgets.ButtonText(new Rect(footer.x, footer.y, buttonWidth, footer.height),
                                               "PR.Tut.Prev".Translate()))
                Advance(-1);

            bool last = page == PageCount - 1;
            string nextLabel = last ? "PR.Tut.Done".Translate() : "PR.Tut.Next".Translate();

            if (Widgets.ButtonText(new Rect(footer.xMax - buttonWidth, footer.y, buttonWidth, footer.height), nextLabel))
                Advance(1);

            if (!last)
            {
                Rect skip = new Rect(footer.xMax - buttonWidth * 2f - 8f, footer.y, buttonWidth, footer.height);
                if (Widgets.ButtonText(skip, "PR.Tut.Skip".Translate())) Close();
            }

            DrawDots(footer);
        }

        private void DrawDots(Rect footer)
        {
            int count = PageCount;
            float width = count * (DotSize + DotGap) - DotGap;
            float x = footer.x + (footer.width - width) / 2f;
            float y = footer.y + (footer.height - DotSize) / 2f;

            for (int i = 0; i < count; i++)
            {
                GUI.color = i == page ? PRTheme.ActiveTurn : DotIdle;
                GUI.DrawTexture(new Rect(x + i * (DotSize + DotGap), y, DotSize, DotSize), PRTextures.Dot);
            }

            GUI.color = Color.white;
        }

        private void Advance(int delta)
        {
            int next = page + delta;

            if (next < 0) return;
            if (next >= PageCount) { Close(); return; }

            page = next;
        }
    }
}
