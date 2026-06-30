using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NoitaCA
{
    public enum SpellElement
    {
        Fire,
        Frost,
        Poison
    }

    public sealed class SpellController : MonoBehaviour
    {
        [Header("Digging")]
        [SerializeField] private int digRadius = 5;
        [SerializeField] private float digCooldown = 0.12f;
        [SerializeField] private int maxDigRangeCells = 72;

        [Header("Spells")]
        [SerializeField] private float projectileSpeed = 9f;
        [SerializeField] private float projectileLifetime = 1.8f;
        [SerializeField] private float normalCooldown = 0.22f;
        [SerializeField] private float specialCooldown = 0.5f;
        [SerializeField] private float ultimateCooldown = 1.1f;
        [SerializeField] private int maxSpellRangeCells = 92;

        private sealed class Projectile
        {
            public SpellElement Element;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Gravity;
            public float TimeRemaining;
            public float TrailTimer;
            public GameObject Visual;
        }

        private readonly List<Projectile> projectiles = new List<Projectile>(32);
        private PixelGrid grid;
        private PixelWorldRenderer worldRenderer;
        private Camera targetCamera;
        private Transform caster;
        private PlayerController casterPlayer;
        private SpellElement selectedElement;
        private float digTimer;
        private float normalTimer;
        private float specialTimer;
        private float ultimateTimer;
        private Sprite fireSprite;
        private Sprite frostSprite;
        private Sprite poisonSprite;
        private Texture2D fireTexture;
        private Texture2D frostTexture;
        private Texture2D poisonTexture;

        public SpellElement SelectedElement => selectedElement;

        public void Initialize(PixelGrid pixelGrid, PixelWorldRenderer renderer, Camera cameraToUse, Transform casterTransform)
        {
            grid = pixelGrid;
            worldRenderer = renderer;
            targetCamera = cameraToUse;
            caster = casterTransform;
            casterPlayer = casterTransform != null ? casterTransform.GetComponent<PlayerController>() : null;
            BuildProjectileSprites();
        }

        private void Update()
        {
            if (grid == null || worldRenderer == null || caster == null)
            {
                return;
            }

            digTimer = Mathf.Max(0f, digTimer - Time.deltaTime);
            normalTimer = Mathf.Max(0f, normalTimer - Time.deltaTime);
            specialTimer = Mathf.Max(0f, specialTimer - Time.deltaTime);
            ultimateTimer = Mathf.Max(0f, ultimateTimer - Time.deltaTime);

            HandleInput();
            StepProjectiles();
        }

        private void HandleInput()
        {
            if (WasCyclePressed())
            {
                selectedElement = (SpellElement)(((int)selectedElement + 1) % 3);
            }

            if (IsSecondaryButtonPressed() && digTimer <= 0f)
            {
                DigAtPointer();
                digTimer = digCooldown;
            }

            if (WasPrimaryButtonPressed())
            {
                if (IsPowerModifierHeld())
                {
                    if (ultimateTimer <= 0f)
                    {
                        CastUltimate();
                        ultimateTimer = ultimateCooldown;
                    }
                }
                else if (IsControlModifierHeld())
                {
                    if (specialTimer <= 0f)
                    {
                        CastSpecial();
                        specialTimer = specialCooldown;
                    }
                }
                else if (normalTimer <= 0f)
                {
                    CastNormal();
                    normalTimer = normalCooldown;
                }
            }
        }

        private void DigAtPointer()
        {
            if (!TryGetAimCell(maxDigRangeCells, out Vector2Int cell))
            {
                return;
            }

            int destroyed = grid.DestroyCircle(cell, digRadius);
            if (destroyed > 0)
            {
                grid.SpawnMaterial(cell, MaterialType.Debris, Mathf.Max(3, digRadius), digRadius + 1);
                TriggerCasterAttack();
            }
        }

        private void CastNormal()
        {
            Vector3 aimWorld = TryGetAimCell(maxSpellRangeCells, out Vector2Int aimCell)
                ? worldRenderer.CellToWorldCenter(aimCell.x, aimCell.y)
                : GetPointerWorldPosition();
            Vector3 origin = caster.position;
            Vector3 direction = aimWorld - origin;
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.right;
            }

            direction.Normalize();
            TriggerCasterAttack();
            Projectile projectile = new Projectile
            {
                Element = selectedElement,
                Position = origin + direction * 0.35f,
                Velocity = direction * projectileSpeed,
                Gravity = 0f,
                TimeRemaining = projectileLifetime,
                TrailTimer = 0f,
                Visual = CreateProjectileVisual(selectedElement)
            };

            if (projectile.Visual != null)
            {
                projectile.Visual.transform.position = projectile.Position;
            }

            projectiles.Add(projectile);
        }

        private void CastSpecial()
        {
            TriggerCasterAttack();
            Vector2Int originCell = worldRenderer.WorldToCell(caster.position);
            Vector2Int aimCell = worldRenderer.WorldToCell(GetPointerWorldPosition());
            Vector2 direction = (Vector2)(aimCell - originCell);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            CastElementLaser(originCell, direction);
        }

        private void CastElementLaser(Vector2Int originCell, Vector2 direction)
        {
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            int range = selectedElement == SpellElement.Fire ? 126 : selectedElement == SpellElement.Frost ? 156 : 138;
            int beamHalfWidth = selectedElement == SpellElement.Frost ? 1 : 2;

            for (int distance = 2; distance <= range; distance++)
            {
                Vector2 center = originCell + direction * distance;
                bool blocked = false;

                for (int widthOffset = -beamHalfWidth; widthOffset <= beamHalfWidth; widthOffset++)
                {
                    Vector2Int cell = new Vector2Int(
                        Mathf.RoundToInt(center.x + perpendicular.x * widthOffset),
                        Mathf.RoundToInt(center.y + perpendicular.y * widthOffset));

                    if (!grid.InBounds(cell.x, cell.y))
                    {
                        blocked = true;
                        continue;
                    }

                    MaterialType material = grid.GetMaterial(cell.x, cell.y);
                    if (grid.IsSolid(cell.x, cell.y))
                    {
                        ApplyLaserImpact(cell, material);
                        blocked = true;
                        continue;
                    }

                    ApplyLaserCell(cell, distance, widthOffset);
                }

                if (blocked && distance > 4)
                {
                    break;
                }
            }
        }

        private void ApplyLaserCell(Vector2Int cell, int distance, int widthOffset)
        {
            if (selectedElement == SpellElement.Fire)
            {
                grid.SetMaterial(cell.x, cell.y, widthOffset == 0 || (distance & 1) == 0 ? MaterialType.Fire : MaterialType.Smoke);
                return;
            }

            if (selectedElement == SpellElement.Frost)
            {
                ExtinguishCircle(cell, 1);
                grid.SetMaterial(cell.x, cell.y, MaterialType.Water);
                if (widthOffset == 0 && (distance % 5) == 0)
                {
                    FreezeSolidEdges(cell, 1);
                }
                return;
            }

            grid.SetMaterial(cell.x, cell.y, MaterialType.Poison);
            if (widthOffset == 0 && (distance % 4) == 0)
            {
                grid.SpawnMaterial(cell, MaterialType.Poison, 3, 1);
            }
        }

        private void ApplyLaserImpact(Vector2Int cell, MaterialType material)
        {
            if (selectedElement == SpellElement.Fire)
            {
                if (material == MaterialType.Wood)
                {
                    grid.IgniteCircle(cell, 4);
                }
                else
                {
                    grid.SpawnMaterial(cell, MaterialType.Fire, 8, 3);
                    grid.SpawnMaterial(cell, MaterialType.Smoke, 6, 3);
                }
                return;
            }

            if (selectedElement == SpellElement.Frost)
            {
                ExtinguishCircle(cell, 4);
                FreezeSolidEdges(cell, 3);
                grid.SpawnMaterial(cell, MaterialType.Water, 12, 3);
                return;
            }

            grid.SpawnMaterial(cell, MaterialType.Poison, 14, 3);
        }

        private void CastUltimate()
        {
            TriggerCasterAttack();
            Vector2Int originCell = worldRenderer.WorldToCell(caster.position);
            Vector2Int aimCell = worldRenderer.WorldToCell(GetPointerWorldPosition());
            Vector2 direction = (Vector2)(aimCell - originCell);
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();
            float baseAngle = Mathf.Atan2(direction.y, direction.x);
            int range = selectedElement == SpellElement.Fire ? 58 : 50;
            int rays = selectedElement == SpellElement.Fire ? 43 : 35;
            float coneRadians = selectedElement == SpellElement.Fire ? 96f * Mathf.Deg2Rad : 78f * Mathf.Deg2Rad;

            for (int ray = 0; ray < rays; ray++)
            {
                float t = rays <= 1 ? 0.5f : ray / (float)(rays - 1);
                float angle = baseAngle + Mathf.Lerp(-coneRadians * 0.5f, coneRadians * 0.5f, t);
                Vector2 rayDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 perpendicular = new Vector2(-rayDirection.y, rayDirection.x);

                for (int distance = 2; distance <= range; distance += 2)
                {
                    Vector2 center = originCell + rayDirection * distance;
                    int forwardSpread = Mathf.Clamp(distance / 12, 0, 4);
                    bool blocked = false;

                    for (int spread = -forwardSpread; spread <= forwardSpread; spread++)
                    {
                        Vector2Int cell = new Vector2Int(
                            Mathf.RoundToInt(center.x + perpendicular.x * spread),
                            Mathf.RoundToInt(center.y + perpendicular.y * spread));

                        if (!grid.InBounds(cell.x, cell.y))
                        {
                            blocked = true;
                            continue;
                        }

                        ApplyUltimateCell(cell, distance);
                        if (grid.IsSolid(cell.x, cell.y) && grid.GetMaterial(cell.x, cell.y) != MaterialType.Wood)
                        {
                            blocked = true;
                        }
                    }

                    if (blocked && distance > 10)
                    {
                        break;
                    }
                }
            }
        }

        private void ApplyUltimateCell(Vector2Int cell, int distance)
        {
            if (selectedElement == SpellElement.Fire)
            {
                if (grid.GetMaterial(cell.x, cell.y) == MaterialType.Wood)
                {
                    grid.IgniteCircle(cell, 3);
                }
                else if (!grid.IsSolid(cell.x, cell.y))
                {
                    grid.SetMaterial(cell.x, cell.y, MaterialType.Fire);
                    if ((distance & 3) == 0)
                    {
                        grid.SpawnMaterial(cell, MaterialType.Smoke, 4, 2);
                    }

                    if (distance > 42 && (distance % 6) == 0)
                    {
                        grid.SpawnMaterial(cell, MaterialType.Fire, 6, 3);
                    }
                }

                return;
            }

            if (selectedElement == SpellElement.Frost)
            {
                ExtinguishCircle(cell, 2);
                if (!grid.IsSolid(cell.x, cell.y))
                {
                    grid.SetMaterial(cell.x, cell.y, MaterialType.Water);
                }

                if ((distance % 5) == 0)
                {
                    FreezeSolidEdges(cell, 2);
                    grid.SpawnMaterial(cell, MaterialType.Water, 5, 2);
                }

                return;
            }

            if (!grid.IsSolid(cell.x, cell.y))
            {
                grid.SetMaterial(cell.x, cell.y, MaterialType.Poison);
                if ((distance % 5) == 0)
                {
                    grid.SpawnMaterial(cell, MaterialType.Poison, 5, 2);
                }
            }
        }

        private void StepProjectiles()
        {
            float cellWorldSize = 1f / Mathf.Max(1, worldRenderer.PixelsPerUnit);
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = projectiles[i];
                projectile.TimeRemaining -= Time.deltaTime;
                projectile.Velocity += Vector3.up * projectile.Gravity * Time.deltaTime;
                Vector3 delta = projectile.Velocity * Time.deltaTime;
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / (cellWorldSize * 0.45f)));
                bool impacted = false;

                for (int stepIndex = 0; stepIndex < steps; stepIndex++)
                {
                    projectile.Position += delta / steps;
                    Vector2Int cell = worldRenderer.WorldToCell(projectile.Position);

                    if (!grid.InBounds(cell.x, cell.y))
                    {
                        impacted = true;
                        break;
                    }

                    LeaveProjectileTrail(projectile, cell);
                    if (grid.IsSolid(cell.x, cell.y))
                    {
                        ResolveImpact(projectile.Element, cell);
                        impacted = true;
                        break;
                    }
                }

                if (!impacted && projectile.TimeRemaining <= 0f)
                {
                    ResolveImpact(projectile.Element, worldRenderer.WorldToCell(projectile.Position));
                    impacted = true;
                }

                if (projectile.Visual != null)
                {
                    projectile.Visual.transform.position = projectile.Position;
                }

                if (impacted)
                {
                    if (projectile.Visual != null)
                    {
                        Destroy(projectile.Visual);
                    }

                    projectiles.RemoveAt(i);
                }
            }
        }

        private void LeaveProjectileTrail(Projectile projectile, Vector2Int cell)
        {
            projectile.TrailTimer -= Time.deltaTime;
            if (projectile.TrailTimer > 0f || !grid.InBounds(cell.x, cell.y) || grid.IsSolid(cell.x, cell.y))
            {
                return;
            }

            projectile.TrailTimer = projectile.Element == SpellElement.Poison ? 0.035f : 0.055f;
            if (projectile.Element == SpellElement.Fire)
            {
                if ((cell.x + cell.y) % 3 == 0)
                {
                    grid.SetMaterial(cell.x, cell.y, MaterialType.Fire);
                }
                else
                {
                    grid.SetMaterial(cell.x, cell.y, MaterialType.Smoke);
                }
            }
            else if (projectile.Element == SpellElement.Frost)
            {
                grid.SetMaterial(cell.x, cell.y, MaterialType.Water);
            }
            else
            {
                grid.SetMaterial(cell.x, cell.y, MaterialType.Poison);
            }
        }

        private void ResolveImpact(SpellElement element, Vector2Int cell)
        {
            if (!grid.InBounds(cell.x, cell.y))
            {
                return;
            }

            if (element == SpellElement.Fire)
            {
                if (grid.GetMaterial(cell.x, cell.y) == MaterialType.Wood)
                {
                    grid.IgniteCircle(cell, 6);
                    grid.SpawnMaterial(cell, MaterialType.Fire, 16, 4);
                    grid.SpawnMaterial(cell, MaterialType.Smoke, 10, 5);
                }
                else
                {
                    grid.ExplodeCircle(cell, 4);
                    grid.IgniteCircle(cell, 6);
                }

                return;
            }

            if (element == SpellElement.Frost)
            {
                ExtinguishCircle(cell, 6);
                grid.SpawnMaterial(cell, MaterialType.Water, 46, 6);
                FreezeSolidEdges(cell, 4);
                return;
            }

            grid.SpawnMaterial(cell, MaterialType.Poison, 56, 6);
            grid.SpawnMaterial(cell, MaterialType.Smoke, 6, 4);
        }

        private void ExtinguishCircle(Vector2Int center, int radius)
        {
            int radiusSquared = radius * radius;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (!grid.InBounds(x, y))
                    {
                        continue;
                    }

                    int dx = x - center.x;
                    int dy = y - center.y;
                    if (dx * dx + dy * dy > radiusSquared)
                    {
                        continue;
                    }

                    MaterialType material = grid.GetMaterial(x, y);
                    if (material == MaterialType.Fire || material == MaterialType.Lava)
                    {
                        grid.SetMaterial(x, y, material == MaterialType.Lava ? MaterialType.Stone : MaterialType.Smoke);
                    }
                }
            }
        }

        private void FreezeSolidEdges(Vector2Int center, int radius)
        {
            int radiusSquared = radius * radius;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (!grid.InBounds(x, y))
                    {
                        continue;
                    }

                    int dx = x - center.x;
                    int dy = y - center.y;
                    if (dx * dx + dy * dy > radiusSquared || grid.IsSolid(x, y))
                    {
                        continue;
                    }

                    if (IsNearSolid(x, y) && ((x + y) & 1) == 0)
                    {
                        grid.SetMaterial(x, y, MaterialType.Ice);
                    }
                }
            }
        }

        private bool IsNearSolid(int x, int y)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if ((ox != 0 || oy != 0) && grid.IsSolid(x + ox, y + oy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetAimCell(int maxRangeCells, out Vector2Int cell)
        {
            cell = worldRenderer.WorldToCell(GetPointerWorldPosition());
            Vector2Int origin = worldRenderer.WorldToCell(caster.position);
            Vector2 delta = cell - origin;
            if (delta.magnitude <= maxRangeCells)
            {
                return true;
            }

            delta = delta.normalized * maxRangeCells;
            cell = new Vector2Int(origin.x + Mathf.RoundToInt(delta.x), origin.y + Mathf.RoundToInt(delta.y));
            return grid.InBounds(cell.x, cell.y);
        }

        private Vector3 GetPointerWorldPosition()
        {
            Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
            Vector3 screenPosition = ReadPointerScreenPosition();
            if (cameraToUse == null)
            {
                return caster.position + Vector3.right;
            }

            screenPosition.z = Mathf.Abs(cameraToUse.transform.position.z - worldRenderer.transform.position.z);
            Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(screenPosition);
            worldPosition.z = caster.position.z;
            return worldPosition;
        }

        private GameObject CreateProjectileVisual(SpellElement element)
        {
            GameObject projectileObject = new GameObject(element + " Projectile");
            projectileObject.transform.SetParent(transform.parent, true);
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetProjectileSprite(element);
            renderer.sortingOrder = 12;
            return projectileObject;
        }

        private Sprite GetProjectileSprite(SpellElement element)
        {
            if (element == SpellElement.Fire)
            {
                return fireSprite;
            }

            return element == SpellElement.Frost ? frostSprite : poisonSprite;
        }

        private void BuildProjectileSprites()
        {
            if (fireSprite != null || worldRenderer == null)
            {
                return;
            }

            fireSprite = BuildProjectileSprite(SpellElement.Fire, out fireTexture, "Fire Projectile Sprite");
            frostSprite = BuildProjectileSprite(SpellElement.Frost, out frostTexture, "Frost Projectile Sprite");
            poisonSprite = BuildProjectileSprite(SpellElement.Poison, out poisonTexture, "Poison Projectile Sprite");
        }

        private Sprite BuildProjectileSprite(SpellElement element, out Texture2D texture, string spriteName)
        {
            const int size = 5;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32[] colors = new Color32[size * size];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = transparent;
            }

            if (element == SpellElement.Fire)
            {
                Color32 hot = new Color32(255, 225, 86, 255);
                Color32 fire = new Color32(255, 96, 26, 255);
                Color32 ember = new Color32(170, 31, 15, 255);
                SetPixel(colors, size, size, 2, 4, hot);
                FillRect(colors, size, size, 1, 2, 3, 2, fire);
                FillRect(colors, size, size, 2, 1, 2, 2, ember);
                SetPixel(colors, size, size, 0, 2, ember);
                SetPixel(colors, size, size, 2, 2, hot);
            }
            else if (element == SpellElement.Frost)
            {
                Color32 water = new Color32(56, 146, 236, 220);
                Color32 shine = new Color32(168, 236, 255, 255);
                FillRect(colors, size, size, 2, 0, 1, 5, water);
                FillRect(colors, size, size, 0, 2, 5, 1, water);
                SetPixel(colors, size, size, 1, 3, shine);
                SetPixel(colors, size, size, 3, 1, shine);
                SetPixel(colors, size, size, 2, 2, shine);
            }
            else
            {
                Color32 poison = new Color32(68, 222, 66, 235);
                Color32 dark = new Color32(26, 116, 38, 255);
                Color32 shine = new Color32(168, 255, 112, 255);
                FillRect(colors, size, size, 1, 1, 3, 3, poison);
                SetPixel(colors, size, size, 2, 4, poison);
                SetPixel(colors, size, size, 0, 2, dark);
                SetPixel(colors, size, size, 4, 2, dark);
                SetPixel(colors, size, size, 2, 2, shine);
            }

            texture.SetPixels32(colors);
            texture.Apply(false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), worldRenderer.PixelsPerUnit);
            sprite.name = spriteName;
            return sprite;
        }

        private static void FillRect(Color32[] colors, int width, int height, int x, int y, int rectWidth, int rectHeight, Color32 color)
        {
            for (int py = y; py < y + rectHeight; py++)
            {
                for (int px = x; px < x + rectWidth; px++)
                {
                    SetPixel(colors, width, height, px, py, color);
                }
            }
        }

        private static void SetPixel(Color32[] colors, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            colors[y * width + x] = color;
        }

        private static Vector3 ReadPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                return new Vector3(position.x, position.y, 0f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.mousePosition;
#else
            return Vector3.zero;
#endif
        }

        private static bool WasPrimaryButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.leftButton.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static bool IsSecondaryButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static bool WasCyclePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.qKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Q);
#else
            return false;
#endif
        }

        private static bool IsControlModifierHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
#else
            return false;
#endif
        }

        private static bool IsPowerModifierHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i].Visual != null)
                {
                    Destroy(projectiles[i].Visual);
                }
            }

            if (fireSprite != null)
            {
                DestroyObject(fireSprite);
            }

            if (frostSprite != null)
            {
                DestroyObject(frostSprite);
            }

            if (poisonSprite != null)
            {
                DestroyObject(poisonSprite);
            }

            if (fireTexture != null)
            {
                DestroyObject(fireTexture);
            }

            if (frostTexture != null)
            {
                DestroyObject(frostTexture);
            }

            if (poisonTexture != null)
            {
                DestroyObject(poisonTexture);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void TriggerCasterAttack()
        {
            if (casterPlayer != null)
            {
                casterPlayer.TriggerAttackAnimation();
            }
        }
    }
}
