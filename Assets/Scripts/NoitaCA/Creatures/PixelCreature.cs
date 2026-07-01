using System.Collections.Generic;
using UnityEngine;

namespace NoitaCA
{
    public sealed class PixelCreature : MonoBehaviour
    {
        private const float MovementSpeedScale = 0.72f;
        private const int MaxStepUpCells = 3;

        private struct OccupiedBodyCell
        {
            public Vector2Int Cell;
            public MaterialType Material;
        }

        private readonly List<OccupiedBodyCell> occupiedCells = new List<OccupiedBodyCell>(32);
        private readonly List<PixelBodyCell> bodyCells = new List<PixelBodyCell>(48);
        private PixelCreatureDefinition definition;
        private PixelGrid grid;
        private PixelWorldRenderer renderer;
        private PlayerController target;
        private Vector2 velocity;
        private float health;
        private float attackTimer;
        private float gaitPhase;
        private float wanderTimer;
        private bool grounded;
        private bool isDying;
        private int facingDirection = 1;
        private int wanderDirection = 1;

        public Vector2Int CenterCell => renderer != null ? renderer.WorldToCell(transform.position) : Vector2Int.zero;
        public float Health => health;
        public PixelCreatureDefinition Definition => definition;

        public void Initialize(PixelCreatureDefinition creatureDefinition, PixelGrid pixelGrid, PixelWorldRenderer worldRenderer, PlayerController player, Vector2Int spawnCell)
        {
            definition = creatureDefinition;
            grid = pixelGrid;
            renderer = worldRenderer;
            target = player;
            health = definition != null ? definition.MaxHealth : 1f;
            velocity = Vector2.zero;
            grounded = false;
            wanderDirection = ((spawnCell.x + spawnCell.y) & 1) == 0 ? 1 : -1;
            facingDirection = wanderDirection;
            ResetWanderTimer(spawnCell);

            if (renderer != null)
            {
                transform.position = renderer.CellToWorldCenter(spawnCell.x, spawnCell.y);
            }

            PixelCreatureRegistry.Register(this);
            RenderBody();
        }

        private void Update()
        {
            if (definition == null || grid == null || renderer == null)
            {
                return;
            }

            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
            ApplyMaterialFeedback();
            if (health <= 0f)
            {
                Die();
                return;
            }

            ClearBody();
            TickMovement();
            AttackPlayerIfTouching();
            RenderBody();
        }

        public void TakeDamage(float damage)
        {
            if (isDying)
            {
                return;
            }

            health -= Mathf.Max(0f, damage);
            if (health <= 0f)
            {
                Die();
            }
        }

        public void AddImpulse(Vector2 impulse)
        {
            velocity += impulse;
        }

        public void Despawn()
        {
            ClearBody();
            Destroy(gameObject);
        }

        private void TickMovement()
        {
            Vector2Int centerCell = CenterCell;
            float desiredX = 0f;
            bool chasingPlayer = false;

            if (target != null)
            {
                Vector2Int playerCell = renderer.WorldToCell(target.transform.position);
                Vector2 toPlayer = playerCell - centerCell;
                if (toPlayer.sqrMagnitude <= definition.DetectionRangeCells * definition.DetectionRangeCells)
                {
                    desiredX = Mathf.Sign(toPlayer.x);
                    chasingPlayer = Mathf.Abs(toPlayer.x) > 0.5f;
                    if (Mathf.Abs(toPlayer.x) > 0.5f)
                    {
                        facingDirection = desiredX >= 0f ? 1 : -1;
                    }

                    if (grounded && (toPlayer.y > 5f || IsBlockedAhead(centerCell)))
                    {
                        velocity.y = definition.JumpSpeed;
                        grounded = false;
                    }
                }
            }

            if (!chasingPlayer)
            {
                desiredX = TickWander(centerCell);
            }

            float scaledMoveSpeed = definition.MoveSpeed * MovementSpeedScale;
            velocity.x = Mathf.MoveTowards(velocity.x, desiredX * scaledMoveSpeed, Time.deltaTime * scaledMoveSpeed * 5f);
            velocity.y += definition.Gravity * Time.deltaTime;
            if (Mathf.Abs(velocity.x) > 0.05f && grounded)
            {
                gaitPhase += Time.deltaTime * definition.GaitFramesPerSecond * Mathf.PI * 2f;
            }

            MoveWithGridCollision(velocity * Time.deltaTime);
        }

        private float TickWander(Vector2Int centerCell)
        {
            wanderTimer -= Time.deltaTime;
            if (grounded && (wanderTimer <= 0f || IsBlockedAhead(centerCell) || !HasGroundAhead(centerCell)))
            {
                wanderDirection *= -1;
                ResetWanderTimer(centerCell);
            }

            facingDirection = wanderDirection;
            return wanderDirection * definition.WanderSpeedMultiplier;
        }

        private void ResetWanderTimer(Vector2Int salt)
        {
            float t = Hash01(salt.x + Mathf.RoundToInt(Time.time * 17f), salt.y + wanderDirection * 31);
            wanderTimer = Mathf.Lerp(definition.WanderTurnIntervalMin, definition.WanderTurnIntervalMax, t);
        }

        private void ApplyMaterialFeedback()
        {
            RectInt bounds = GetBounds(CenterCell);
            float strongestDamagePerSecond = 0f;
            float liquidPush = 0f;

            for (int y = bounds.yMin - 1; y <= bounds.yMax + 1; y++)
            {
                for (int x = bounds.xMin - 1; x <= bounds.xMax + 1; x++)
                {
                    if (!grid.InBounds(x, y))
                    {
                        continue;
                    }

                    if (IsOccupiedCell(new Vector2Int(x, y)))
                    {
                        continue;
                    }

                    MaterialDefinition material = MaterialDatabase.Get(grid.GetMaterial(x, y));
                    strongestDamagePerSecond = Mathf.Max(strongestDamagePerSecond, material.PlayerDamagePerSecond);
                    if (material.MovementMode == PixelMovementMode.Liquid)
                    {
                        liquidPush += x < bounds.center.x ? 1f : -1f;
                    }
                }
            }

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                OccupiedBodyCell occupied = occupiedCells[i];
                Vector2Int cell = occupied.Cell;
                if (!grid.InBounds(cell.x, cell.y))
                {
                    health -= definition.BodyLossDamage * Time.deltaTime;
                    continue;
                }

                MaterialType material = grid.GetMaterial(cell.x, cell.y);
                if (material != occupied.Material)
                {
                    health -= definition.BodyLossDamage * Time.deltaTime;
                }
            }

            if (strongestDamagePerSecond > 0f)
            {
                health -= strongestDamagePerSecond * Time.deltaTime;
            }

            if (Mathf.Abs(liquidPush) > 0.01f)
            {
                velocity.x += Mathf.Clamp(liquidPush, -1f, 1f) * Time.deltaTime * 7f;
                velocity.y += Time.deltaTime * 1.4f;
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

            float cellWorldSize = 1f / Mathf.Max(1, renderer.PixelsPerUnit);
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

            for (int y = 1; y <= MaxStepUpCells; y++)
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
            Vector2Int center = renderer.WorldToCell(worldPosition);
            BuildCurrentBody();
            for (int i = 0; i < bodyCells.Count; i++)
            {
                Vector2Int cell = center + new Vector2Int(bodyCells[i].Offset.x * facingDirection, bodyCells[i].Offset.y);
                if (grid.IsSolid(cell.x, cell.y) && !IsOccupiedCell(cell))
                {
                    return true;
                }
            }

            if (OverlapsPlayer(worldPosition))
            {
                return true;
            }

            return false;
        }

        private bool OverlapsPlayer(Vector3 worldPosition)
        {
            if (target == null)
            {
                return false;
            }

            RectInt playerBounds = target.GetCoveredCellsAt(target.transform.position);
            Vector2Int center = renderer.WorldToCell(worldPosition);
            for (int i = 0; i < bodyCells.Count; i++)
            {
                Vector2Int cell = center + new Vector2Int(bodyCells[i].Offset.x * facingDirection, bodyCells[i].Offset.y);
                if (playerBounds.Contains(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlockedAhead(Vector2Int centerCell)
        {
            RectInt bounds = GetBounds(centerCell);
            int x = facingDirection > 0 ? bounds.xMax + 1 : bounds.xMin - 1;
            return grid.IsSolid(x, bounds.yMin) || grid.IsSolid(x, bounds.yMin + 1);
        }

        private bool HasGroundAhead(Vector2Int centerCell)
        {
            RectInt bounds = GetBounds(centerCell);
            int x = facingDirection > 0 ? bounds.xMax + 1 : bounds.xMin - 1;
            int y = bounds.yMin - 1;
            return grid.IsSolid(x, y) || grid.IsSolid(x - facingDirection, y);
        }

        private void AttackPlayerIfTouching()
        {
            if (target == null || attackTimer > 0f)
            {
                return;
            }

            if (!OverlapsPlayer(transform.position))
            {
                return;
            }

            target.ApplyDamage(definition.ContactDamage, true, true);
            attackTimer = 0.65f;

            Vector2 knockback = new Vector2(facingDirection * 6f, 4f);
            velocity += knockback;
            grounded = false;
        }

        private void RenderBody()
        {
            occupiedCells.Clear();
            Vector2Int center = CenterCell;
            BuildCurrentBody();
            for (int i = 0; i < bodyCells.Count; i++)
            {
                Vector2Int offset = new Vector2Int(bodyCells[i].Offset.x * facingDirection, bodyCells[i].Offset.y);
                Vector2Int cell = center + offset;
                if (!grid.InBounds(cell.x, cell.y))
                {
                    continue;
                }

                Pixel currentPixel = grid.GetCell(cell.x, cell.y);
                if (currentPixel.IsCreatureBody && !IsOccupiedCell(cell))
                {
                    continue;
                }

                MaterialDefinition current = MaterialDatabase.Get(currentPixel.MaterialType);
                if (current.IsAir || current.CanBeDisplaced || current.PlayerDamagePerSecond > 0f)
                {
                    grid.SetCreatureBodyMaterialSilent(cell.x, cell.y, bodyCells[i].Material);
                    occupiedCells.Add(new OccupiedBodyCell { Cell = cell, Material = bodyCells[i].Material });
                }
            }
        }

        private void ClearBody()
        {
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i].Cell;
                if (grid.InBounds(cell.x, cell.y) && grid.GetCell(cell.x, cell.y).IsCreatureBody)
                {
                    grid.SetMaterialSilent(cell.x, cell.y, MaterialType.Air);
                }
            }

            occupiedCells.Clear();
        }

        private bool IsOccupiedCell(Vector2Int cell)
        {
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                if (occupiedCells[i].Cell == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private RectInt GetBounds(Vector2Int center)
        {
            BuildCurrentBody();
            if (bodyCells.Count == 0)
            {
                return new RectInt(center.x, center.y, 1, 1);
            }

            int minX = center.x;
            int maxX = center.x;
            int minY = center.y;
            int maxY = center.y;
            for (int i = 0; i < bodyCells.Count; i++)
            {
                int x = center.x + bodyCells[i].Offset.x * facingDirection;
                int y = center.y + bodyCells[i].Offset.y;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void BuildCurrentBody()
        {
            bool moving = Mathf.Abs(velocity.x) > 0.05f;
            definition.BuildBodyCells(bodyCells, gaitPhase, moving, grounded);
        }

        private void Die()
        {
            if (isDying)
            {
                return;
            }

            isDying = true;
            if (grid != null)
            {
                ClearBody();
                Vector2Int center = CenterCell;
                SpawnRewardVisualEffect(center);
                PixelCreatureDrop[] drops = definition != null ? definition.Drops : null;
                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        if (drops[i].Material != MaterialType.Air && drops[i].Amount > 0)
                        {
                            grid.SpawnMaterial(center, drops[i].Material, drops[i].Amount, Mathf.Max(1, drops[i].Radius));
                        }

                        if (drops[i].Equipment != null)
                        {
                            PixelWorldSpawner.SpawnEquipmentPickup(drops[i].Equipment, grid, renderer, target, center, transform.parent);
                        }
                    }
                }

                grid.SpawnMaterial(center, MaterialType.Smoke, 8, 4);
            }

            Destroy(gameObject);
        }

        private void SpawnRewardVisualEffect(Vector2Int centerCell)
        {
            if (renderer == null)
            {
                return;
            }

            Vector3 worldPosition = renderer.CellToWorldCenter(centerCell.x, centerCell.y);
            PixelRewardBurstEffect.Spawn(worldPosition, transform.parent, renderer.PixelsPerUnit);
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                ClearBody();
            }

            PixelCreatureRegistry.Unregister(this);
        }

        private static float Hash01(int a, int b)
        {
            unchecked
            {
                uint n = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                n ^= n >> 13;
                n *= 1274126177u;
                n ^= n >> 16;
                return (n & 0x00FFFFFF) / 16777215f;
            }
        }
    }
}
