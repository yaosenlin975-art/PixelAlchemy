using UnityEngine;
using UnityEngine.UI;

namespace Lin.Runtime.Helper
{
    public static class GraphicExtensions
    {
        public static Graphic SetColorR(this Graphic graphic, float colorR)
        {
            graphic.color = graphic.color.WithR(colorR);
            return graphic;
        }

        public static Graphic SetColorG(this Graphic graphic, float colorG)
        {
            graphic.color = graphic.color.WithG(colorG);
            return graphic;
        }

        public static Graphic SetColorB(this Graphic graphic, float colorB)
        {
            graphic.color = graphic.color.WithB(colorB);
            return graphic;
        }

        public static Graphic SetColorA(this Graphic graphic, float colorA)
        {
            graphic.color = graphic.color.WithA(colorA);
            return graphic;
        }

        public static Graphic SetColorRGB(
            this Graphic graphic,
            float colorR,
            float colorG,
            float colorB
        )
        {
            graphic.color = graphic.color.WithRGB(colorR, colorG, colorB);
            return graphic;
        }

        public static Graphic SetColorRGB(
            this Graphic graphic,
            float colorR,
            float colorG,
            float colorB,
            float colorA
        )
        {
            graphic.color = graphic.color.WithRGB(colorR, colorG, colorB).WithA(colorA);
            return graphic;
        }

        public static Graphic SetColorRGB(this Graphic graphic, Color color)
        {
            graphic.color = color.WithA(graphic.color.a);
            return graphic;
        }

        public static T SetAnchorPosition<T>(this T self, float? x = null, float? y = null) where T : Graphic
        {
            self.rectTransform.SetAnchorPosition(x, y);
            return self;
        }

        public static T SetAnchorPositionX<T>(this T self, float x) where T : Graphic
        {
            self.rectTransform.SetAnchorPositionX(x);
            return self;
        }

        public static T SetAnchorPositionY<T>(this T self, float y) where T : Graphic
        {
            self.rectTransform.SetAnchorPositionY(y);
            return self;
        }

        public static T AddAnchorPosition<T>(this T self, Vector2 offset) where T : Graphic
        {
            self.rectTransform.AddAnchorPosition(offset);
            return self;
        }

        public static T AddAnchorPositionX<T>(this T self, float x) where T : Graphic
        {
            self.rectTransform.AddAnchorPositionX(x);
            return self;
        }

        public static T AddAnchorPositionY<T>(this T self, float y) where T : Graphic
        {
            self.rectTransform.AddAnchorPositionY(y);
            return self;
        }

        public static T SetSize<T>(this T self, float? width = null, float? height = null) where T : Graphic
        {
            self.rectTransform.SetSize(width, height);
            return self;
        }

        public static T SetWidth<T>(this T self, float width) where T : Graphic
        {
            self.rectTransform.SetWidth(width);
            return self;
        }

        public static T SetHeight<T>(this T self, float height) where T : Graphic
        {
            self.rectTransform.SetHeight(height);
            return self;
        }

        public static float GetWidth<T>(this T self) where T : Graphic
        {
            return self.rectTransform.GetWidth();
        }

        public static float GetHeight<T>(this T self) where T : Graphic
        {
            return self.rectTransform.GetHeight();
        }
    }
}
