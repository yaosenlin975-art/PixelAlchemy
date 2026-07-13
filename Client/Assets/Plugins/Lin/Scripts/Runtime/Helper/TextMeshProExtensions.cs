/*
┌────────────────────────────┐
│　Description: TMP + DOTween
│　Remark: 
└────────────────────────────┘
*/
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Lin.Runtime.Helper
{
    public static class TextMeshProExtensions
    {
        /// <summary>Tweens a Text's text to the given value.
        /// Also stores the Text as the tween's target so it can be used for filtered operations</summary>
        /// <param name="endValue">The end string to tween to</param><param name="duration">The duration of the tween</param>
        /// <param name="richTextEnabled">If TRUE (default), rich text will be interpreted correctly while animated,
        /// otherwise all tags will be considered as normal text</param>
        /// <param name="scrambleMode">The type of scramble mode to use, if any</param>
        /// <param name="scrambleChars">A string containing the characters to use for scrambling.
        /// Use as many characters as possible (minimum 10) because DOTween uses a fast scramble mode which gives better results with more characters.
        /// Leave it to NULL (default) to use default ones</param>
        public static TweenerCore<string, string, StringOptions> DOText(this TMP_Text target, string endValue, float duration, bool richTextEnabled = true, ScrambleMode scrambleMode = ScrambleMode.None, string scrambleChars = null)
        {
            if (endValue == null)
            {
                if (Debugger.logPriority > 0) Debugger.LogWarning("You can't pass a NULL string to DOText: an empty string will be used instead to avoid errors");
                endValue = "";
            }
            TweenerCore<string, string, StringOptions> t = DOTween.To(() => target.text, x => target.text = x, endValue, duration);
            t.SetOptions(richTextEnabled, scrambleMode, scrambleChars)
                .SetTarget(target)
                .AutoRecycle();

            return t;
        }

        public static Vector2 RenderedSize(this TMP_Text self) => new Vector2(self.renderedWidth, self.renderedHeight);

        public static TMP_InputField AddOnValueChangedListener(this TMP_InputField self, UnityAction<string> callback)
        {
            self.onValueChanged.AddListener(callback);
            return self;
        }

        public static TMP_InputField AddOnEndEditListener(this TMP_InputField self, UnityAction<string> callback)
        {
            self.onEndEdit.AddListener(callback);
            return self;
        }

        public static TMP_InputField RemoveOnValueChangedListener(this TMP_InputField self, UnityAction<string> callback)
        {
            self.onValueChanged.RemoveListener(callback);
            return self;
        }

        public static TMP_InputField RemoveOnEndEditListener(this TMP_InputField self, UnityAction<string> callback)
        {
            self.onEndEdit.RemoveListener(callback);
            return self;
        }
    }
}