using Cysharp.Text;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Lin.Runtime.Helper
{
    public static class UIExtensions
    {
        public static InputField AddOnValueChangedListener(this InputField self, UnityAction<string> listener)
        {
            self.onValueChanged.AddListener(listener);
            return self;
        }

        public static InputField RemoveOnValueChangedListener(this InputField self, UnityAction<string> listener)
        {
            try
            {
                self.onValueChanged.RemoveListener(listener);
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }

        public static InputField AddOnEndEditListener(this InputField self, UnityAction<string> listener)
        {
            self.onEndEdit.AddListener(listener);
            return self;
        }

        public static InputField RemoveOnEndEditListener(this InputField self, UnityAction<string> listener)
        {
            try
            {
                self.onEndEdit.RemoveListener(listener);
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }

        public static Button AddOnClickListener(this Button self, UnityAction listener)
        {
            self.onClick.AddListener(listener);
            return self;
        }

        public static Button RemoveAllClickListeners(this Button self)
        {
            try
            {
                self.onClick.RemoveAllListeners();
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }

        public static Button RemoveOnClickListener(this Button self, UnityAction listener)
        {
            try
            {
                self.onClick.RemoveListener(listener);
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }

        public static Toggle AddOnValueChangedListener(this Toggle self, UnityAction<bool> listener)
        {
            self.onValueChanged.AddListener(listener);
            return self;
        }

        public static Toggle RemoveOnValueChangedListener(this Toggle self, UnityAction<bool> listener)
        {
            try
            {
                self.onValueChanged.RemoveListener(listener);
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }

        public static Slider AddOnValueChangedListener(this Slider self, UnityAction<float> listener)
        {
            self.onValueChanged.AddListener(listener);
            return self;
        }

        public static Slider RemoveOnValueChangedListener(this Slider self, UnityAction<float> listener)
        {
            try
            {
                self.onValueChanged.RemoveListener(listener);
            }
            catch (System.Exception e)
            {
                self.Error(ZString.Concat("移除监听失败, ", e.Message));
            }
            return self;
        }
    }
}
