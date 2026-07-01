using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NoitaCA
{
    public enum PlayerAnimationState
    {
        Idle,
        Walk,
        Run,
        Jump,
        Attack
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float runSpeedMultiplier = 1.45f;
        [SerializeField] private float jumpSpeed = 7.2f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private int maxStepUpCells = 3;

        [Header("Size")]
        [SerializeField] private int widthInCells = 7;
        [SerializeField] private int heightInCells = 14;
        [SerializeField] private float visualHeightInCells = 18f;

        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityAfterRespawn = 0.8f;

        [Header("Visuals")]
        [SerializeField] private bool useProceduralSprites = true;
        [SerializeField] private Sprite playerSprite;
        [SerializeField] private float idleFramesPerSecond = 4f;
        [SerializeField] private float walkFramesPerSecond = 8f;
        [SerializeField] private float runFramesPerSecond = 12f;
        [SerializeField] private float attackFramesPerSecond = 16f;
        [SerializeField] private float attackDuration = 0.28f;

        private PixelGrid grid;
        private PixelWorldRenderer worldRenderer;
        private Camera targetCamera;
        private SpriteRenderer spriteRenderer;
        private SpellController spellController;
        private PixelEquipmentController equipmentController;
        private readonly List<Sprite> generatedSprites = new List<Sprite>(32);
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>(32);
        private Sprite[] idleFrames;
        private Sprite[] walkFrames;
        private Sprite[] runFrames;
        private Sprite[] jumpFrames;
        private Sprite[] attackFrames;
        private Vector2 velocity;
        private Vector2Int spawnCell;
        private Vector3 spawnWorldPosition;
        private bool grounded;
        private bool running;
        private float health;
        private float invulnerableTimer;
        private float materialSpeedMultiplier = 1f;
        private PlayerAnimationState animationState = PlayerAnimationState.Idle;
        private float animationTimer;
        private float attackTimer;
        private float hitFlashTimer;
        private const float HitFlashDuration = 0.42f;
        private int animationFrameIndex;
        private int facingDirection = 1;
        private float visualScale = 1f;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public bool IsDead => health <= 0f;
        public PixelGrid Grid => grid;
        public PixelWorldRenderer WorldRenderer => worldRenderer;
        public Camera TargetCamera => targetCamera;
        public int WidthInCells => widthInCells;
        public int HeightInCells => heightInCells;

        public RectInt GetCoveredCellsAt(Vector3 worldPosition)
        {
            float ppu = Mathf.Max(1, worldRenderer.PixelsPerUnit);
            Vector2 halfSize = new Vector2(widthInCells / ppu, heightInCells / ppu) * 0.5f;
            Vector2 min = (Vector2)worldPosition - halfSize;
            Vector2 max = (Vector2)worldPosition + halfSize;
            Vector2Int minCell = worldRenderer.WorldToCell(min);
            Vector2Int maxCell = worldRenderer.WorldToCell(max);
            return new RectInt(minCell.x, minCell.y, maxCell.x - minCell.x, maxCell.y - minCell.y);
        }

        public RectInt CurrentCoveredCells => GetCoveredCellsAt(transform.position);

        public void ConfigureSize(int collisionWidthInCells, int collisionHeightInCells, float spriteHeightInCells)
        {
            widthInCells = Mathf.Max(1, collisionWidthInCells);
            heightInCells = Mathf.Max(1, collisionHeightInCells);
            visualHeightInCells = Mathf.Max(1f, spriteHeightInCells);

            if (spriteRenderer != null && worldRenderer != null)
            {
                ApplyVisualScale();
            }
        }

        public void TriggerAttackAnimation()
        {
            attackTimer = Mathf.Max(0.05f, attackDuration);
            SetAnimationState(PlayerAnimationState.Attack, true);
        }

        public void Initialize(PixelGrid pixelGrid, PixelWorldRenderer renderer, Camera cameraToUse, Vector2Int playerSpawnCell, Sprite defaultSprite = null)
        {
            grid = pixelGrid;
            worldRenderer = renderer;
            targetCamera = cameraToUse;
            spawnCell = playerSpawnCell;
            spawnWorldPosition = worldRenderer.CellToWorldCenter(spawnCell.x, spawnCell.y);
            health = Mathf.Max(1f, maxHealth);
            invulnerableTimer = invulnerabilityAfterRespawn;
            velocity = Vector2.zero;
            grounded = false;

            spriteRenderer = GetComponent<SpriteRenderer>();
            playerSprite = defaultSprite != null ? defaultSprite : playerSprite;
            BuildSprite();
            ApplyVisualScale();
            transform.position = spawnWorldPosition;

            if (!TryGetComponent(out spellController))
            {
                spellController = gameObject.AddComponent<SpellController>();
            }

            spellController.Initialize(grid, worldRenderer, targetCamera, transform);

            if (!TryGetComponent(out equipmentController))
            {
                equipmentController = gameObject.AddComponent<PixelEquipmentController>();
            }

            equipmentController.Initialize(spellController);
        }

        public void Initialize(PixelGrid pixelGrid, PixelWorldRenderer renderer, Vector2Int playerSpawnCell)
        {
            Initialize(pixelGrid, renderer, Camera.main, playerSpawnCell, playerSprite);
        }

        private void Update()
        {
            if (grid == null || worldRenderer == null)
            {
                return;
            }

            invulnerableTimer = Mathf.Max(0f, invulnerableTimer - Time.deltaTime);
            ApplyMaterialFeedback();

            float horizontal = ReadHorizontal();
            running = Mathf.Abs(horizontal) > 0.01f && IsRunHeld();
            float speedMultiplier = running ? runSpeedMultiplier : 1f;
            velocity.x = horizontal * moveSpeed * speedMultiplier * materialSpeedMultiplier;
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                facingDirection = horizontal > 0f ? 1 : -1;
                ApplyVisualScale();
            }

            if (grounded && IsJumpPressed())
            {
                velocity.y = jumpSpeed;
                grounded = false;
            }

            velocity.y += gravity * Time.deltaTime;
            MoveWithGridCollision(velocity * Time.deltaTime);
            RespawnIfOutOfWorld();
            UpdateProceduralAnimation();
            UpdateHitFlash();
        }

        public void Respawn()
        {
            health = Mathf.Max(1f, maxHealth);
            invulnerableTimer = invulnerabilityAfterRespawn;
            velocity = Vector2.zero;
            grounded = false;
            attackTimer = 0f;
            hitFlashTimer = 0f;
            SetSpriteColor(Color.white);
            transform.position = spawnWorldPosition;
        }

        public void WarpToCell(Vector2Int targetCell, bool updateSpawnPoint)
        {
            Vector3 targetWorldPosition = worldRenderer.CellToWorldCenter(targetCell.x, targetCell.y);
            velocity = Vector2.zero;
            grounded = false;
            attackTimer = 0f;
            invulnerableTimer = Mathf.Max(invulnerableTimer, invulnerabilityAfterRespawn * 0.5f);
            transform.position = targetWorldPosition;

            if (updateSpawnPoint)
            {
                spawnCell = targetCell;
                spawnWorldPosition = targetWorldPosition;
            }
        }

        public void SetRespawnCell(Vector2Int targetCell)
        {
            spawnCell = targetCell;
            spawnWorldPosition = worldRenderer.CellToWorldCenter(targetCell.x, targetCell.y);
        }

        public void ApplyDamage(float damage, bool respectInvulnerability)
        {
            ApplyDamage(damage, respectInvulnerability, false);
        }

        public void ApplyDamage(float damage, bool respectInvulnerability, bool showHitFeedback)
        {
            if (damage <= 0f || IsDead || (respectInvulnerability && invulnerableTimer > 0f))
            {
                return;
            }

            float appliedDamage = Mathf.Min(health, damage);
            health -= damage;
            if (showHitFeedback)
            {
                TriggerHitFeedback(appliedDamage);
            }

            if (health <= 0f)
            {
                Respawn();
            }
        }

        private void TriggerHitFeedback(float damage)
        {
            hitFlashTimer = HitFlashDuration;
            SpawnDamageNumber(damage);
        }

        private void UpdateHitFlash()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (hitFlashTimer <= 0f)
            {
                SetSpriteColor(Color.white);
                return;
            }

            hitFlashTimer = Mathf.Max(0f, hitFlashTimer - Time.deltaTime);
            float phase = Mathf.PingPong((HitFlashDuration - hitFlashTimer) * 18f, 1f);
            Color flashColor = Color.Lerp(new Color(1f, 0.28f, 0.22f, 1f), Color.white, phase);
            SetSpriteColor(flashColor);
        }

        private void SpawnDamageNumber(float damage)
        {
            if (worldRenderer == null)
            {
                return;
            }

            float ppu = Mathf.Max(1, worldRenderer.PixelsPerUnit);
            Vector3 position = transform.position + Vector3.up * (heightInCells / ppu * 0.72f + 0.18f);
            int sortingLayerId = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            int sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 6 : 16;
            PlayerDamageNumberFeedback.Spawn(
                position,
                transform.parent,
                Mathf.Max(1, worldRenderer.PixelsPerUnit),
                damage,
                sortingLayerId,
                sortingOrder);
        }

        private void SetSpriteColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        private void ApplyMaterialFeedback()
        {
            RectInt bounds = GetCoveredCells(transform.position);
            float strongestSlow = 1f;
            float damagePerSecond = 0f;

            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x <= bounds.xMax; x++)
                {
                    if (!grid.InBounds(x, y))
                    {
                        continue;
                    }

                    Pixel pixel = grid.GetCell(x, y);
                    MaterialDefinition definition = MaterialDatabase.Get(pixel.MaterialType);
                    strongestSlow = Mathf.Min(strongestSlow, definition.PlayerSpeedMultiplier);
                    if (!pixel.IsPlayerSpell)
                    {
                        damagePerSecond = Mathf.Max(damagePerSecond, definition.PlayerDamagePerSecond);
                    }
                }
            }

            materialSpeedMultiplier = strongestSlow;
            if (damagePerSecond > 0f && invulnerableTimer <= 0f)
            {
                ApplyDamage(damagePerSecond * Time.deltaTime, false);
            }
        }

        private void RespawnIfOutOfWorld()
        {
            Vector2Int cell = worldRenderer.WorldToCell(transform.position);
            if (cell.y < -heightInCells || cell.x < -widthInCells || cell.x > grid.Width + widthInCells)
            {
                Respawn();
            }
        }

        private void MoveWithGridCollision(Vector2 delta)
        {
            bool canStepUp = grounded;
            grounded = false;
            MoveAxis(new Vector2(delta.x, 0f), canStepUp);
            MoveAxis(new Vector2(0f, delta.y), false);
        }

        private void MoveAxis(Vector2 delta, bool canStepUp)
        {
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            float cellWorldSize = 1f / Mathf.Max(1, worldRenderer.PixelsPerUnit);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / (cellWorldSize * 0.45f)));
            Vector2 step = delta / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector3 nextPosition = transform.position + (Vector3)step;
                if (OverlapsSolid(nextPosition))
                {
                    if (Mathf.Abs(step.y) > Mathf.Abs(step.x))
                    {
                        if (step.y < 0f)
                        {
                            grounded = true;
                        }

                        velocity.y = 0f;
                    }
                    else
                    {
                        if (canStepUp && TryStepUp(step, cellWorldSize))
                        {
                            continue;
                        }

                        velocity.x = 0f;
                    }

                    return;
                }

                transform.position = nextPosition;
            }
        }

        private bool TryStepUp(Vector2 horizontalStep, float cellWorldSize)
        {
            if (Mathf.Abs(horizontalStep.x) <= 0.0001f)
            {
                return false;
            }

            int safeStepCells = Mathf.Max(0, maxStepUpCells);
            for (int y = 1; y <= safeStepCells; y++)
            {
                Vector3 steppedPosition = transform.position + new Vector3(horizontalStep.x, y * cellWorldSize, 0f);
                if (!OverlapsSolid(steppedPosition))
                {
                    transform.position = steppedPosition;
                    grounded = false;
                    velocity.y = Mathf.Max(0f, velocity.y);
                    return true;
                }
            }

            return false;
        }

        private bool OverlapsSolid(Vector3 worldPosition)
        {
            RectInt bounds = GetCoveredCells(worldPosition);
            for (int y = bounds.yMin; y <= bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x <= bounds.xMax; x++)
                {
                    if (grid.IsSolid(x, y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private RectInt GetCoveredCells(Vector3 worldPosition)
        {
            float ppu = Mathf.Max(1, worldRenderer.PixelsPerUnit);
            Vector2 halfSize = new Vector2(widthInCells / ppu, heightInCells / ppu) * 0.5f;
            Vector2 min = (Vector2)worldPosition - halfSize;
            Vector2 max = (Vector2)worldPosition + halfSize;
            Vector2Int minCell = worldRenderer.WorldToCell(min);
            Vector2Int maxCell = worldRenderer.WorldToCell(max);
            return new RectInt(minCell.x, minCell.y, maxCell.x - minCell.x, maxCell.y - minCell.y);
        }

        private void BuildSprite()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            ReleaseGeneratedSprites();

            if (!useProceduralSprites && playerSprite != null)
            {
                spriteRenderer.sprite = playerSprite;
                spriteRenderer.sortingOrder = 10;
                return;
            }

            idleFrames = BuildAnimationFrames(PlayerAnimationState.Idle, 4);
            walkFrames = BuildAnimationFrames(PlayerAnimationState.Walk, 4);
            runFrames = BuildAnimationFrames(PlayerAnimationState.Run, 4);
            jumpFrames = BuildAnimationFrames(PlayerAnimationState.Jump, 1);
            attackFrames = BuildAnimationFrames(PlayerAnimationState.Attack, 4);
            spriteRenderer.sprite = idleFrames[0];
            spriteRenderer.sortingOrder = 10;
            SetAnimationState(PlayerAnimationState.Idle, true);
        }

        private void ApplyVisualScale()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null || worldRenderer == null)
            {
                return;
            }

            float spriteWorldHeight = spriteRenderer.sprite.rect.height / Mathf.Max(1f, spriteRenderer.sprite.pixelsPerUnit);
            float targetWorldHeight = visualHeightInCells / Mathf.Max(1, worldRenderer.PixelsPerUnit);
            visualScale = spriteWorldHeight > 0f ? targetWorldHeight / spriteWorldHeight : 1f;
            transform.localScale = new Vector3(visualScale * facingDirection, visualScale, 1f);
        }

        private Sprite[] BuildAnimationFrames(PlayerAnimationState state, int frameCount)
        {
            Sprite[] frames = new Sprite[Mathf.Max(1, frameCount)];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = BuildProceduralFrame(state, i);
            }

            return frames;
        }

        private Sprite BuildProceduralFrame(PlayerAnimationState state, int frame)
        {
            const int width = 16;
            const int height = 24;
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32[] colors = new Color32[width * height];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = transparent;
            }

            DrawCharacter(colors, width, height, state, frame);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Procedural Player " + state + " " + frame;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(colors);
            texture.Apply(false);
            generatedTextures.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), worldRenderer.PixelsPerUnit);
            sprite.name = texture.name;
            generatedSprites.Add(sprite);
            return sprite;
        }

        private static void DrawCharacter(Color32[] colors, int width, int height, PlayerAnimationState state, int frame)
        {
            Color32 outline = new Color32(15, 17, 22, 255);
            Color32 robe = new Color32(54, 76, 143, 255);
            Color32 robeLight = new Color32(91, 132, 206, 255);
            Color32 robeDark = new Color32(28, 37, 84, 255);
            Color32 trim = new Color32(235, 190, 82, 255);
            Color32 gem = new Color32(107, 229, 255, 255);
            Color32 skin = new Color32(230, 184, 126, 255);
            Color32 hair = new Color32(78, 49, 32, 255);
            Color32 eye = new Color32(124, 223, 255, 255);
            Color32 boot = new Color32(33, 27, 29, 255);
            Color32 wand = new Color32(112, 74, 38, 255);
            Color32 wandTip = new Color32(188, 143, 72, 255);
            Color32 spark = new Color32(255, 214, 88, 255);

            int bob = state == PlayerAnimationState.Idle ? (frame == 1 || frame == 2 ? 1 : 0) : 0;
            int stride = frame % 4;
            int bodyShift = 0;
            int leftLegX = 5;
            int rightLegX = 9;
            int leftLegY = 1;
            int rightLegY = 1;
            int armLeftY = 10;
            int armRightY = 10;
            bool attacking = state == PlayerAnimationState.Attack;

            if (state == PlayerAnimationState.Walk)
            {
                bodyShift = stride == 1 ? 1 : stride == 3 ? -1 : 0;
                leftLegX += stride == 1 ? -1 : stride == 3 ? 1 : 0;
                rightLegX += stride == 1 ? 1 : stride == 3 ? -1 : 0;
                armLeftY += stride == 1 ? 1 : stride == 3 ? -1 : 0;
                armRightY += stride == 1 ? -1 : stride == 3 ? 1 : 0;
            }
            else if (state == PlayerAnimationState.Run)
            {
                bodyShift = stride == 1 ? 1 : stride == 3 ? -1 : 0;
                leftLegX += stride == 1 ? -2 : stride == 3 ? 2 : 0;
                rightLegX += stride == 1 ? 2 : stride == 3 ? -2 : 0;
                leftLegY += stride == 0 || stride == 2 ? 1 : 0;
                rightLegY += stride == 0 || stride == 2 ? 0 : 1;
                armLeftY += stride == 1 ? 2 : stride == 3 ? -2 : 0;
                armRightY += stride == 1 ? -2 : stride == 3 ? 2 : 0;
            }
            else if (state == PlayerAnimationState.Jump)
            {
                bob = 1;
                leftLegX = 4;
                rightLegX = 10;
                leftLegY = 3;
                rightLegY = 2;
                armLeftY = 12;
                armRightY = 12;
            }
            else if (attacking)
            {
                bob = frame == 2 ? 1 : 0;
                armRightY = frame < 2 ? 13 : 11;
                armLeftY = 9;
            }

            int x = bodyShift;
            int y = bob;

            FillRect(colors, width, height, leftLegX + x, leftLegY, 2, 5, outline);
            FillRect(colors, width, height, rightLegX + x, rightLegY, 2, 5, outline);
            FillRect(colors, width, height, leftLegX + x, leftLegY + 1, 1, 3, robeDark);
            FillRect(colors, width, height, rightLegX + x, rightLegY + 1, 1, 3, robeDark);
            FillRect(colors, width, height, leftLegX + x - 1, leftLegY, 3, 1, boot);
            FillRect(colors, width, height, rightLegX + x, rightLegY, 3, 1, boot);

            FillRect(colors, width, height, 3 + x, 4 + y, 10, 1, outline);
            FillRect(colors, width, height, 4 + x, 5 + y, 8, 8, outline);
            FillRect(colors, width, height, 5 + x, 6 + y, 6, 6, robe);
            FillRect(colors, width, height, 6 + x, 7 + y, 2, 5, robeLight);
            FillRect(colors, width, height, 10 + x, 6 + y, 1, 5, robeDark);
            FillRect(colors, width, height, 4 + x, 5 + y, 8, 1, robeDark);
            FillRect(colors, width, height, 5 + x, 6 + y, 6, 1, trim);
            FillRect(colors, width, height, 7 + x, 6 + y, 2, 5, trim);
            SetPixel(colors, width, height, 8 + x, 9 + y, gem);
            SetPixel(colors, width, height, 8 + x, 11 + y, trim);

            FillRect(colors, width, height, 2 + x, armLeftY + y, 3, 2, outline);
            FillRect(colors, width, height, 3 + x, armLeftY + y, 2, 1, robe);
            FillRect(colors, width, height, 11 + x, armRightY + y, attacking ? 4 : 3, 2, outline);
            FillRect(colors, width, height, 11 + x, armRightY + y, attacking ? 3 : 2, 1, robeLight);

            if (attacking)
            {
                int wandY = armRightY + y + (frame < 2 ? 2 : 0);
                FillRect(colors, width, height, 13 + x, wandY, 1, 5, wand);
                SetPixel(colors, width, height, 13 + x, wandY + 5, wandTip);
                SetPixel(colors, width, height, 14 + x, wandY + 5, spark);
                SetPixel(colors, width, height, 15 + x, wandY + 4, spark);
                SetPixel(colors, width, height, 14 + x, wandY + 3, spark);
                SetPixel(colors, width, height, 12 + x, wandY + 4, spark);
            }
            else
            {
                FillRect(colors, width, height, 13 + x, 7 + y, 1, 8, wand);
                SetPixel(colors, width, height, 13 + x, 15 + y, wandTip);
            }

            FillRect(colors, width, height, 5 + x, 13 + y, 6, 6, outline);
            FillRect(colors, width, height, 6 + x, 14 + y, 4, 4, skin);
            FillRect(colors, width, height, 5 + x, 17 + y, 6, 2, hair);
            SetPixel(colors, width, height, 9 + x, 16 + y, eye);
            SetPixel(colors, width, height, 7 + x, 15 + y, new Color32(184, 120, 78, 255));

            FillRect(colors, width, height, 3 + x, 18 + y, 10, 2, outline);
            FillRect(colors, width, height, 4 + x, 19 + y, 8, 1, robeDark);
            FillRect(colors, width, height, 5 + x, 20 + y, 6, 1, outline);
            FillRect(colors, width, height, 6 + x, 21 + y, 4, 1, robeDark);
            FillRect(colors, width, height, 7 + x, 22 + y, 2, 1, robeDark);
            SetPixel(colors, width, height, 8 + x, 23 + y, trim);
            SetPixel(colors, width, height, 6 + x, 20 + y, trim);
            SetPixel(colors, width, height, 10 + x, 19 + y, trim);
        }

        private void UpdateProceduralAnimation()
        {
            if (spriteRenderer == null || idleFrames == null)
            {
                return;
            }

            if (attackTimer > 0f)
            {
                attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
                SetAnimationState(PlayerAnimationState.Attack, false);
            }
            else if (!grounded)
            {
                SetAnimationState(PlayerAnimationState.Jump, false);
            }
            else if (Mathf.Abs(velocity.x) > 0.05f)
            {
                SetAnimationState(running ? PlayerAnimationState.Run : PlayerAnimationState.Walk, false);
            }
            else
            {
                SetAnimationState(PlayerAnimationState.Idle, false);
            }

            Sprite[] frames = GetFrames(animationState);
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            float fps = GetAnimationFps(animationState);
            animationTimer += Time.deltaTime;
            if (animationTimer >= 1f / Mathf.Max(1f, fps))
            {
                animationTimer = 0f;
                animationFrameIndex = (animationFrameIndex + 1) % frames.Length;
            }

            spriteRenderer.sprite = frames[Mathf.Clamp(animationFrameIndex, 0, frames.Length - 1)];
        }

        private void SetAnimationState(PlayerAnimationState nextState, bool forceRestart)
        {
            if (!forceRestart && animationState == nextState)
            {
                return;
            }

            animationState = nextState;
            animationTimer = 0f;
            animationFrameIndex = 0;
        }

        private Sprite[] GetFrames(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Walk:
                    return walkFrames;
                case PlayerAnimationState.Run:
                    return runFrames;
                case PlayerAnimationState.Jump:
                    return jumpFrames;
                case PlayerAnimationState.Attack:
                    return attackFrames;
                default:
                    return idleFrames;
            }
        }

        private float GetAnimationFps(PlayerAnimationState state)
        {
            switch (state)
            {
                case PlayerAnimationState.Walk:
                    return walkFramesPerSecond;
                case PlayerAnimationState.Run:
                    return runFramesPerSecond;
                case PlayerAnimationState.Attack:
                    return attackFramesPerSecond;
                case PlayerAnimationState.Jump:
                    return 1f;
                default:
                    return idleFramesPerSecond;
            }
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

        private static float ReadHorizontal()
        {
            float horizontal = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                {
                    horizontal -= 1f;
                }

                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                {
                    horizontal += 1f;
                }

                return Mathf.Clamp(horizontal, -1f, 1f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
#endif
            return Mathf.Clamp(horizontal, -1f, 1f);
        }

        private static bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return Keyboard.current.spaceKey.wasPressedThisFrame
                    || Keyboard.current.wKey.wasPressedThisFrame
                    || Keyboard.current.upArrowKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Space)
                || UnityEngine.Input.GetKeyDown(KeyCode.W)
                || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            ReleaseGeneratedSprites();
        }

        private void ReleaseGeneratedSprites()
        {
            for (int i = 0; i < generatedSprites.Count; i++)
            {
                if (generatedSprites[i] != null)
                {
                    DestroyObject(generatedSprites[i]);
                }
            }

            for (int i = 0; i < generatedTextures.Count; i++)
            {
                if (generatedTextures[i] != null)
                {
                    DestroyObject(generatedTextures[i]);
                }
            }

            generatedSprites.Clear();
            generatedTextures.Clear();
            idleFrames = null;
            walkFrames = null;
            runFrames = null;
            jumpFrames = null;
            attackFrames = null;
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

        private static bool IsRunHeld()
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
    }
}
