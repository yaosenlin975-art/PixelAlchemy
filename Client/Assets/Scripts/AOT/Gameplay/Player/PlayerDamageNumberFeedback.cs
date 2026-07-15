// 职责：玩家伤害数字反馈，显示飘字伤害效果。
// Responsibility: Floating damage number feedback display for player damage.
using UnityEngine;

namespace AOT
{
    public sealed class PlayerDamageNumberFeedback : MonoBehaviour
    {
        [SerializeField] private GameObject _damageTextPrefab;
        [SerializeField] private float _floatSpeed = 1f;
        [SerializeField] private float _fadeDuration = 1f;

        private class DamageNumber
        {
            public GameObject GameObject;
            public TextMesh TextMesh;
            public float Timer;
        }

        private System.Collections.Generic.List<DamageNumber> _activeNumbers;

        private void Awake()
        {
            _activeNumbers = new System.Collections.Generic.List<DamageNumber>();
        }

        public void ShowDamage(int damage, Vector3 position)
        {
            if (_damageTextPrefab == null)
            {
                GameObject textObj = new GameObject("DamageNumber");
                textObj.transform.position = position;
                TextMesh textMesh = textObj.AddComponent<TextMesh>();
                textMesh.text = damage.ToString();
                textMesh.fontSize = 12;
                textMesh.color = Color.red;
                textMesh.anchor = TextAnchor.MiddleCenter;

                DamageNumber dn = new DamageNumber
                {
                    GameObject = textObj,
                    TextMesh = textMesh,
                    Timer = 0f
                };
                _activeNumbers.Add(dn);
            }
        }

        private void Update()
        {
            for (int i = _activeNumbers.Count - 1; i >= 0; i--)
            {
                DamageNumber dn = _activeNumbers[i];
                dn.Timer += Time.deltaTime;

                Vector3 pos = dn.GameObject.transform.position;
                pos.y += _floatSpeed * Time.deltaTime;
                dn.GameObject.transform.position = pos;

                Color color = dn.TextMesh.color;
                color.a = Mathf.Lerp(1f, 0f, dn.Timer / _fadeDuration);
                dn.TextMesh.color = color;

                if (dn.Timer >= _fadeDuration)
                {
                    Destroy(dn.GameObject);
                    _activeNumbers.RemoveAt(i);
                }
            }
        }
    }
}
