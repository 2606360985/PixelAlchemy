using System.Collections.Generic;
using UnityEngine;

namespace NoitaCA
{
    public sealed class PixelWorldSpawner : MonoBehaviour
    {
        private readonly List<GameObject> spawnedObjects = new List<GameObject>(64);
        private readonly List<PixelWorldSpawnPoint> monsterSpawnPoints = new List<PixelWorldSpawnPoint>(16);
        private readonly List<PixelWorldSpawnPoint> itemSpawnPoints = new List<PixelWorldSpawnPoint>(16);
        private PixelGrid grid;
        private PixelWorldRenderer renderer;
        private PlayerController player;
        private PixelCreatureDefinition[] creatureDefinitions;
        private PixelEquipmentDefinition[] equipmentDefinitions;
        private int minMonstersPerSegment = 2;
        private int maxMonstersPerSegment = 5;
        private float respawnInterval = 8f;
        private float respawnTimer;
        private int lastSpawnSegment;
        private int respawnCounter;

        public void Initialize(
            PixelGrid pixelGrid,
            PixelWorldRenderer worldRenderer,
            PlayerController targetPlayer,
            PixelCreatureDefinition[] configuredCreatures,
            PixelEquipmentDefinition[] configuredEquipment)
        {
            grid = pixelGrid;
            renderer = worldRenderer;
            player = targetPlayer;
            equipmentDefinitions = configuredEquipment != null && configuredEquipment.Length > 0
                ? configuredEquipment
                : BuildRuntimeEquipment();
            creatureDefinitions = configuredCreatures != null && configuredCreatures.Length > 0
                ? configuredCreatures
                : BuildRuntimeCreatures(equipmentDefinitions);
        }

        public void SpawnFromPoints(IReadOnlyList<PixelWorldSpawnPoint> spawnPoints, int activeSegment)
        {
            ClearSpawnedObjects();
            lastSpawnSegment = activeSegment;
            respawnTimer = respawnInterval;
            respawnCounter = 0;
            if (grid == null || renderer == null || spawnPoints == null)
            {
                return;
            }

            monsterSpawnPoints.Clear();
            itemSpawnPoints.Clear();
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                PixelWorldSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint.SegmentIndex != activeSegment)
                {
                    continue;
                }

                if (spawnPoint.Category == PixelWorldSpawnCategory.Monster)
                {
                    monsterSpawnPoints.Add(spawnPoint);
                }
                else if (spawnPoint.Category == PixelWorldSpawnCategory.Item || spawnPoint.Category == PixelWorldSpawnCategory.Treasure)
                {
                    itemSpawnPoints.Add(spawnPoint);
                }
                else if (spawnPoint.Category == PixelWorldSpawnCategory.Ambient)
                {
                    grid.SpawnMaterial(spawnPoint.Cell, MaterialType.Smoke, 6, 3);
                }
            }

            SpawnSegmentMonsters(activeSegment);
            for (int i = 0; i < itemSpawnPoints.Count; i++)
            {
                SpawnEquipment(itemSpawnPoints[i], activeSegment + i);
            }
        }

        public void SpawnIntroContent(Vector2Int originCell, int monsterCount, int equipmentCount)
        {
            if (grid == null || renderer == null)
            {
                return;
            }

            int safeMonsterCount = Mathf.Max(0, monsterCount);
            int safeEquipmentCount = Mathf.Max(0, equipmentCount);
            for (int i = 0; i < safeMonsterCount; i++)
            {
                int direction = i % 2 == 0 ? 1 : -1;
                int distance = 14 + i * 8;
                Vector2Int requested = new Vector2Int(originCell.x + direction * distance, originCell.y);
                if (TryFindNearbyFloorCell(requested, 18, out Vector2Int spawnCell))
                {
                    SpawnCreature(spawnCell, i);
                }
            }

            for (int i = 0; i < safeEquipmentCount; i++)
            {
                Vector2Int requested = new Vector2Int(originCell.x + 8 + i * 6, originCell.y + 4);
                if (TryFindNearbyFloorCell(requested, 14, out Vector2Int spawnCell))
                {
                    SpawnEquipment(spawnCell, i);
                }
            }
        }

        public void ClearSpawnedObjects()
        {
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] != null)
                {
                    if (spawnedObjects[i].TryGetComponent(out PixelCreature creature))
                    {
                        creature.Despawn();
                    }
                    else
                    {
                        Destroy(spawnedObjects[i]);
                    }
                }
            }

            spawnedObjects.Clear();
        }

        public static PixelEquipmentPickup SpawnEquipmentPickup(
            PixelEquipmentDefinition equipment,
            PixelGrid grid,
            PixelWorldRenderer renderer,
            PlayerController player,
            Vector2Int cell,
            Transform parent)
        {
            if (equipment == null || renderer == null)
            {
                return null;
            }

            GameObject pickupObject = new GameObject(equipment.DisplayName + " Pickup");
            pickupObject.transform.SetParent(parent, true);
            pickupObject.AddComponent<SpriteRenderer>();
            PixelEquipmentPickup pickup = pickupObject.AddComponent<PixelEquipmentPickup>();
            pickup.Initialize(equipment, grid, renderer, player, cell);
            return pickup;
        }

        private void SpawnCreature(PixelWorldSpawnPoint spawnPoint, int index)
        {
            SpawnCreature(spawnPoint.Cell, index);
        }

        private void SpawnCreature(Vector2Int cell, int index)
        {
            if (creatureDefinitions == null || creatureDefinitions.Length == 0)
            {
                return;
            }

            PixelCreatureDefinition definition = creatureDefinitions[PositiveModulo(index, creatureDefinitions.Length)];
            GameObject creatureObject = new GameObject(definition.DisplayName);
            creatureObject.transform.SetParent(transform, true);
            PixelCreature creature = creatureObject.AddComponent<PixelCreature>();
            creature.Initialize(definition, grid, renderer, player, cell);
            spawnedObjects.Add(creatureObject);
        }

        private void SpawnEquipment(PixelWorldSpawnPoint spawnPoint, int index)
        {
            SpawnEquipment(spawnPoint.Cell, index);
        }

        private void SpawnEquipment(Vector2Int cell, int index)
        {
            if (equipmentDefinitions == null || equipmentDefinitions.Length == 0)
            {
                return;
            }

            PixelEquipmentDefinition definition = equipmentDefinitions[PositiveModulo(index, equipmentDefinitions.Length)];
            PixelEquipmentPickup pickup = SpawnEquipmentPickup(definition, grid, renderer, player, cell, transform);
            if (pickup != null)
            {
                spawnedObjects.Add(pickup.gameObject);
            }
        }

        private void SpawnSegmentMonsters(int activeSegment)
        {
            if (monsterSpawnPoints.Count == 0)
            {
                return;
            }

            int targetCount = Mathf.Clamp(
                minMonstersPerSegment + Mathf.FloorToInt(Hash01(activeSegment, 41) * (maxMonstersPerSegment - minMonstersPerSegment + 1)),
                minMonstersPerSegment,
                maxMonstersPerSegment);
            targetCount = Mathf.Min(targetCount, monsterSpawnPoints.Count);

            int start = Mathf.FloorToInt(Hash01(activeSegment, 73) * monsterSpawnPoints.Count);
            int step = Mathf.Max(1, monsterSpawnPoints.Count / Mathf.Max(1, targetCount));
            int spawned = 0;

            for (int i = 0; spawned < targetCount && i < monsterSpawnPoints.Count; i++)
            {
                int pointIndex = (start + i * step) % monsterSpawnPoints.Count;
                SpawnCreature(monsterSpawnPoints[pointIndex], activeSegment * 17 + spawned);
                spawned++;
            }
        }

        private bool TryFindNearbyFloorCell(Vector2Int requestedCell, int searchRadius, out Vector2Int spawnCell)
        {
            spawnCell = requestedCell;
            int safeRadius = Mathf.Max(1, searchRadius);
            for (int dx = 0; dx <= safeRadius; dx++)
            {
                int[] xCandidates = dx == 0
                    ? new[] { requestedCell.x }
                    : new[] { requestedCell.x - dx, requestedCell.x + dx };

                for (int i = 0; i < xCandidates.Length; i++)
                {
                    int x = xCandidates[i];
                    for (int y = requestedCell.y + safeRadius; y >= requestedCell.y - safeRadius; y--)
                    {
                        if (IsSpawnableFloorCell(x, y))
                        {
                            spawnCell = new Vector2Int(x, y);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsSpawnableFloorCell(int x, int y)
        {
            if (!grid.InBounds(x, y) || !grid.InBounds(x, y - 1) || !grid.IsSolid(x, y - 1))
            {
                return false;
            }

            int halfWidth = player != null ? Mathf.Max(1, player.WidthInCells / 2) : 2;
            int clearHeight = player != null ? Mathf.Max(1, player.HeightInCells) : 10;
            for (int ox = -halfWidth; ox <= halfWidth; ox++)
            {
                for (int oy = 0; oy < clearHeight; oy++)
                {
                    if (!grid.InBounds(x + ox, y + oy) || !MaterialDatabase.Get(grid.GetMaterial(x + ox, y + oy)).IsAir)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static PixelEquipmentDefinition[] BuildRuntimeEquipment()
        {
            PixelAbility[] abilities = PixelAbilitySet.CreateRuntimeDefaults();
            PixelEquipmentDefinition[] equipment = new PixelEquipmentDefinition[abilities.Length];
            for (int i = 0; i < abilities.Length; i++)
            {
                equipment[i] = PixelEquipmentDefinition.CreateRuntime(abilities[i].DisplayName, abilities[i]);
            }

            return equipment;
        }

        private static PixelCreatureDefinition[] BuildRuntimeCreatures(PixelEquipmentDefinition[] equipment)
        {
            PixelEquipmentDefinition rareA = equipment != null && equipment.Length > 2 ? equipment[2] : null;
            PixelEquipmentDefinition rareB = equipment != null && equipment.Length > 3 ? equipment[3] : null;
            PixelEquipmentDefinition rareC = equipment != null && equipment.Length > 4 ? equipment[4] : null;
            return new PixelCreatureDefinition[]
            {
                PixelCreatureDefinition.CreateRuntimeCrawler(rareA),
                PixelCreatureDefinition.CreateRuntimeJumper(rareB),
                PixelCreatureDefinition.CreateRuntimeEmber(rareC),
                PixelCreatureDefinition.CreateRuntimeComposite("Ash Walker", PixelCreatureBodyPlan.Biped, MaterialType.Ash, MaterialType.Smoke, MaterialType.Debris, MaterialType.Fire, 30f, 2.3f, 5.8f, rareA),
                PixelCreatureDefinition.CreateRuntimeComposite("Poison Hound", PixelCreatureBodyPlan.Quadruped, MaterialType.Poison, MaterialType.Smoke, MaterialType.Poison, MaterialType.Fire, 46f, 2.5f, 5.2f, rareB),
                PixelCreatureDefinition.CreateRuntimeComposite("Stoneback", PixelCreatureBodyPlan.Quadruped, MaterialType.Debris, MaterialType.Stone, MaterialType.Wood, MaterialType.Fire, 64f, 1.4f, 4.2f, rareC),
                PixelCreatureDefinition.CreateRuntimeComposite("Water Strider", PixelCreatureBodyPlan.Biped, MaterialType.Water, MaterialType.Ice, MaterialType.Water, MaterialType.Fire, 36f, 2.9f, 6.4f, rareB)
            };
        }

        private static int PositiveModulo(int value, int modulo)
        {
            return modulo <= 0 ? 0 : ((value % modulo) + modulo) % modulo;
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

        private void Update()
        {
            if (grid == null || renderer == null || monsterSpawnPoints.Count == 0)
            {
                return;
            }

            CleanDestroyedObjects();
            int aliveMonsters = CountAliveCreatures();
            if (aliveMonsters >= minMonstersPerSegment)
            {
                respawnTimer = respawnInterval;
                return;
            }

            respawnTimer -= Time.deltaTime;
            if (respawnTimer > 0f)
            {
                return;
            }

            respawnTimer = respawnInterval;
            int pointIndex = respawnCounter % monsterSpawnPoints.Count;
            respawnCounter++;
            PixelWorldSpawnPoint point = monsterSpawnPoints[pointIndex];
            if (IsSpawnableFloorCell(point.Cell.x, point.Cell.y))
            {
                SpawnCreature(point.Cell, lastSpawnSegment * 17 + respawnCounter);
            }
        }

        private void CleanDestroyedObjects()
        {
            for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            {
                if (spawnedObjects[i] == null)
                {
                    spawnedObjects.RemoveAt(i);
                }
            }
        }

        private int CountAliveCreatures()
        {
            int count = 0;
            for (int i = 0; i < spawnedObjects.Count; i++)
            {
                if (spawnedObjects[i] != null && spawnedObjects[i].GetComponent<PixelCreature>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnDestroy()
        {
            ClearSpawnedObjects();
        }
    }
}
