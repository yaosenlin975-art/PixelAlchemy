// 职责：玩家 MonoBehaviour 外壳，负责输入采集 + ECS 数据同步。
// Responsibility: Player MonoBehaviour shell for input capture + ECS data sync.
using Unity.Entities;
using UnityEngine;

namespace AOT
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private int _playerId;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _playerColor = Color.white;

        private Entity _playerEntity;
        private EntityManager _entityManager;
        private PlayerData _cachedData;

        public int PlayerId
        {
            get { return _playerId; }
        }

        public Entity PlayerEntity
        {
            get { return _playerEntity; }
        }

        public void Initialize(Entity playerEntity, EntityManager entityManager)
        {
            _playerEntity = playerEntity;
            _entityManager = entityManager;
            _cachedData = entityManager.GetComponentData<PlayerData>(playerEntity);

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            _spriteRenderer.color = _playerColor;

            // 创建简单像素风格精灵
            Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] colors = new Color32[64];
            for (int i = 0; i < 64; i++)
            {
                colors[i] = _playerColor;
            }
            tex.SetPixels32(colors);
            tex.Apply();
            _spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }

        public void CaptureInput()
        {
            if (!_entityManager.Exists(_playerEntity))
                return;

            PlayerData data = _entityManager.GetComponentData<PlayerData>(_playerEntity);

            data.ActionFlags = 0;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                data.ActionFlags |= 0x0001;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                data.ActionFlags |= 0x0002;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space))
                data.ActionFlags |= 0x0004;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                data.ActionFlags |= 0x0008;
            if (Input.GetMouseButton(0))
                data.ActionFlags |= 0x0010;
            if (Input.GetMouseButton(1))
                data.ActionFlags |= 0x0020;

            // 瞄准角度
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 playerPos = transform.position;
            float angle = Mathf.Atan2(mousePos.y - playerPos.y, mousePos.x - playerPos.x);
            if (angle < 0) angle += Mathf.PI * 2;
            data.AimAngle = (ushort)(angle * 100f);

            _entityManager.SetComponentData(_playerEntity, data);
            _cachedData = data;
        }

        public void SyncFromECS()
        {
            if (!_entityManager.Exists(_playerEntity))
                return;

            _cachedData = _entityManager.GetComponentData<PlayerData>(_playerEntity);

            float x = _cachedData.Position.X.ToFloat();
            float y = _cachedData.Position.Y.ToFloat();
            transform.position = new Vector3(x, y, 0f);
        }

        private void Update()
        {
            SyncFromECS();
        }
    }
}
