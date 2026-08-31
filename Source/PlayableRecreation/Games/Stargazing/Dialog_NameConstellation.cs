using System;
using UnityEngine;
using Verse;

namespace Stargazing
{
    /// <summary>이어 둔 선에 이름을 붙인다. 이름표는 세계에 남는다.</summary>
    public class Dialog_NameConstellation : Window
    {
        private readonly Action<string> accept;
        private string name;
        private bool focused;

        public override Vector2 InitialSize
        {
            get { return new Vector2(420f, 180f); }
        }

        public Dialog_NameConstellation(string suggestion, Action<string> accept)
        {
            this.accept = accept;
            name = suggestion;

            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "STG.Name.Title".Translate());
            Text.Font = GameFont.Small;

            Rect field = new Rect(inRect.x, inRect.y + 44f, inRect.width, 30f);

            GUI.SetNextControlName("PR_ConstellationName");
            name = Widgets.TextField(field, name);

            if (!focused)
            {
                focused = true;
                UI.FocusControl("PR_ConstellationName", this);
            }

            if (name != null && name.Length > 40) name = name.Substring(0, 40);

            Rect ok = new Rect(inRect.xMax - 140f, inRect.yMax - 36f, 140f, 34f);
            Rect cancel = new Rect(ok.x - 148f, ok.y, 140f, 34f);

            if (Widgets.ButtonText(cancel, "STG.Name.Cancel".Translate())) Close();

            bool enter = Event.current.type == EventType.KeyDown
                      && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            if (!Widgets.ButtonText(ok, "STG.Name.Accept".Translate()) && !enter) return;
            if (name.NullOrEmpty()) return;

            if (enter) Event.current.Use();

            if (accept != null) accept(name.Trim());
            Close();
        }
    }
}
