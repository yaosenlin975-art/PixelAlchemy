using UnityEngine;

namespace Lin.Runtime.Helper
{

    public static class RectTransformExtensions
    {
        #region - AnchorPosition -

        public static RectTransform SetAnchorPosition(this RectTransform self, float? x = null, float? y = null)
        {
            var position = self.anchoredPosition;

            if (x.HasValue)
                position.x = x.Value;

            if (y.HasValue)
                position.y = y.Value;

            self.anchoredPosition = position;
            return self;
        }

        public static RectTransform SetAnchorPositionX(this RectTransform self, float x) => self.SetAnchorPosition(x);

        public static RectTransform SetAnchorPositionY(this RectTransform self, float y) => self.SetAnchorPosition(null, y);

        public static RectTransform AddAnchorPosition(this RectTransform self, Vector2 offset)
        {
            var position = self.anchoredPosition + offset;
            self.anchoredPosition = position;
            return self;
        }

        public static RectTransform AddAnchorPositionX(this RectTransform self, float x)
        {
            var position = self.anchoredPosition;
            position.WithAddX(x);
            self.anchoredPosition = position;
            return self;
        }

        public static RectTransform AddAnchorPositionY(this RectTransform self, float y)
        {
            var position = self.anchoredPosition;
            position.WithAddY(y);
            self.anchoredPosition = position;
            return self;
        }

        #endregion

        #region - Size -

        public static RectTransform SetSize(this RectTransform self, float? width = null, float? height = null)
        {
            var size = self.sizeDelta;
            if (width.HasValue)
                size.x = width.Value;
            if (height.HasValue)
                size.y = height.Value;
            self.sizeDelta = size;
            return self;
        }

        public static RectTransform SetWidth(this RectTransform self, float width)
        {
            var size = self.sizeDelta;
            size.WithX(width);
            self.sizeDelta = size;
            return self;
        }

        public static RectTransform SetHeight(this RectTransform self, float height)
        {
            var size = self.sizeDelta;
            size.WithY(height);
            self.sizeDelta = size;
            return self;
        }

        /// <summary>
        /// 获取RectTransform的实际宽度，包括stretch状态下的宽度
        /// </summary>
        public static float GetWidth(this RectTransform self)
        {
            // 如果锚点在水平方向上是stretch状态（anchorMin.x != anchorMax.x）
            if (Mathf.Abs(self.anchorMin.x - self.anchorMax.x) > 0.001f)
            {
                // 在stretch状态下，实际宽度 = 父容器宽度 * stretch比例 + sizeDelta.x
                var parentRect = self.parent as RectTransform;
                if (parentRect != null)
                {
                    float parentWidth = parentRect.rect.width;
                    float stretchWidth = parentWidth * (self.anchorMax.x - self.anchorMin.x);
                    return stretchWidth + self.sizeDelta.x;
                }
            }
            // 非stretch状态或无父容器时，返回sizeDelta.x
            return self.sizeDelta.x;
        }

        /// <summary>
        /// 获取RectTransform的实际高度，包括stretch状态下的高度
        /// </summary>
        public static float GetHeight(this RectTransform self)
        {
            // 如果锚点在垂直方向上是stretch状态（anchorMin.y != anchorMax.y）
            if (Mathf.Abs(self.anchorMin.y - self.anchorMax.y) > 0.001f)
            {
                // 在stretch状态下，实际高度 = 父容器高度 * stretch比例 + sizeDelta.y
                var parentRect = self.parent as RectTransform;
                if (parentRect != null)
                {
                    float parentHeight = parentRect.rect.height;
                    float stretchHeight = parentHeight * (self.anchorMax.y - self.anchorMin.y);
                    return stretchHeight + self.sizeDelta.y;
                }
            }
            // 非stretch状态或无父容器时，返回sizeDelta.y
            return self.sizeDelta.y;
        }

        #endregion
    }
}
