/*
┌────────────────────────────┐
│　Description: TMP + DOTween
│　Remark: 
└────────────────────────────┘
*/
using DG.Tweening;

namespace Lin.Runtime.Helper
{
    public static class DOTweenExtensions 
    {
        public static T AutoRecycle<T>(this T self, bool recyclable = true, bool autoKill = true) where T : Tween => self.SetRecyclable(recyclable).SetAutoKill(autoKill);
    }
}