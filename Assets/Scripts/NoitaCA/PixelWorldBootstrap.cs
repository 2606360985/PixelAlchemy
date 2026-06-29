using UnityEngine;

namespace NoitaCA
{
    [DefaultExecutionOrder(-100)]
    public sealed class PixelWorldBootstrap : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private int worldWidth = 256;
        [SerializeField] private int worldHeight = 144;
        [SerializeField] private int pixelsPerUnit = 16;
        [SerializeField] private int simulationStepsPerFrame = 1;
        [SerializeField] private float simulationStepInterval = 0.035f;
        [SerializeField] private float cameraPadding = 0.5f;

        [Header("Demo")]
        [SerializeField] private bool buildDemoTerrain = true;
        [SerializeField] private bool buildDemoPlayer = true;
        [SerializeField] private int bottomlessHoleCenterX = -1;
        [SerializeField] private int bottomlessHoleWidth = 10;

        private PixelGrid grid;
        private PixelSimulation simulation;
        private PixelWorldRenderer worldRenderer;
        private InputController inputController;
        private SimplePixelPlayer player;
        private Camera targetCamera;
        private float simulationAccumulator;
        private Vector2Int playerSpawnCell;

        private void Awake()
        {
            BuildWorld();
        }

        private void Update()
        {
            if (grid == null || simulation == null || worldRenderer == null || inputController == null)
            {
                return;
            }

            inputController.Tick();

            simulationAccumulator += Time.deltaTime;
            float safeStepInterval = Mathf.Max(0.001f, simulationStepInterval);
            int simulatedTicks = 0;

            while (simulationAccumulator >= safeStepInterval && simulatedTicks < 4)
            {
                int steps = Mathf.Max(1, simulationStepsPerFrame);
                for (int i = 0; i < steps; i++)
                {
                    simulation.Step(grid);
                }

                simulationAccumulator -= safeStepInterval;
                simulatedTicks++;
            }

            if (simulatedTicks >= 4)
            {
                simulationAccumulator = 0f;
            }

            worldRenderer.Render();
        }

        private void BuildWorld()
        {
            grid = new PixelGrid(worldWidth, worldHeight);
            simulation = new PixelSimulation();

            if (buildDemoTerrain)
            {
                BuildDemoTerrain();
            }

            targetCamera = GetOrCreateCamera();
            worldRenderer = GetOrCreateRenderer();
            inputController = GetOrCreateInputController();

            ConfigureDisplayTransform();
            worldRenderer.Initialize(grid, pixelsPerUnit);
            ConfigureCamera();
            inputController.Initialize(grid, worldRenderer, targetCamera);

            if (buildDemoPlayer)
            {
                player = GetOrCreatePlayer();
                player.Initialize(grid, worldRenderer, playerSpawnCell);
            }

            worldRenderer.Render();
        }

        private PixelWorldRenderer GetOrCreateRenderer()
        {
            Transform existingDisplay = transform.Find("Pixel World Display");
            GameObject displayObject;

            if (existingDisplay == null)
            {
                displayObject = new GameObject("Pixel World Display");
                displayObject.transform.SetParent(transform, false);
            }
            else
            {
                displayObject = existingDisplay.gameObject;
            }

            if (!displayObject.TryGetComponent(out SpriteRenderer _))
            {
                displayObject.AddComponent<SpriteRenderer>();
            }

            if (!displayObject.TryGetComponent(out PixelWorldRenderer renderer))
            {
                renderer = displayObject.AddComponent<PixelWorldRenderer>();
            }

            return renderer;
        }

        private InputController GetOrCreateInputController()
        {
            if (!TryGetComponent(out InputController controller))
            {
                controller = gameObject.AddComponent<InputController>();
            }

            return controller;
        }

        private SimplePixelPlayer GetOrCreatePlayer()
        {
            Transform existingPlayer = transform.Find("Demo Player");
            GameObject playerObject;

            if (existingPlayer == null)
            {
                playerObject = new GameObject("Demo Player");
                playerObject.transform.SetParent(transform, false);
            }
            else
            {
                playerObject = existingPlayer.gameObject;
            }

            if (!playerObject.TryGetComponent(out SpriteRenderer _))
            {
                playerObject.AddComponent<SpriteRenderer>();
            }

            if (!playerObject.TryGetComponent(out SimplePixelPlayer playerController))
            {
                playerController = playerObject.AddComponent<SimplePixelPlayer>();
            }

            return playerController;
        }

        private Camera GetOrCreateCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                return camera;
            }

            camera = Object.FindObjectOfType<Camera>();
            if (camera != null)
            {
                return camera;
            }

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private void ConfigureDisplayTransform()
        {
            Vector2 worldSize = new Vector2(worldWidth / (float)pixelsPerUnit, worldHeight / (float)pixelsPerUnit);
            worldRenderer.transform.position = new Vector3(-worldSize.x * 0.5f, -worldSize.y * 0.5f, 0f);
            worldRenderer.transform.rotation = Quaternion.identity;
            worldRenderer.transform.localScale = Vector3.one;
        }

        private void ConfigureCamera()
        {
            Vector2 worldSize = new Vector2(worldWidth / (float)pixelsPerUnit, worldHeight / (float)pixelsPerUnit);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = worldSize.y * 0.5f + Mathf.Max(0f, cameraPadding);
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.transform.rotation = Quaternion.identity;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.015f, 0.018f, 0.025f, 1f);
        }

        private void BuildDemoTerrain()
        {
            int[] surfaceHeights = new int[worldWidth];

            for (int x = 0; x < worldWidth; x++)
            {
                surfaceHeights[x] = GetTerrainHeight(x);

                for (int y = 0; y < surfaceHeights[x]; y++)
                {
                    grid.SetMaterial(x, y, MaterialType.Stone);
                }
            }

            CarveArena(surfaceHeights);
            CarveBottomlessHole();
            SeedWater(surfaceHeights);
            SeedSand(surfaceHeights);
            BuildWoodenChainReaction(surfaceHeights);
            SeedSmokePocket(surfaceHeights);
            playerSpawnCell = new Vector2Int(Mathf.Clamp(worldWidth / 7, 4, worldWidth - 5), Mathf.Min(worldHeight - 10, surfaceHeights[worldWidth / 7] + 12));
        }

        private int GetTerrainHeight(int x)
        {
            float t = worldWidth <= 1 ? 0f : x / (float)(worldWidth - 1);
            float descendingSlope = Mathf.Lerp(worldHeight * 0.42f, worldHeight * 0.2f, t);
            float longWave = Mathf.Sin(t * Mathf.PI * 3.4f) * 7f;
            float shortWave = Mathf.Sin(t * Mathf.PI * 12.5f + 0.7f) * 3f;
            float centerBasin = -16f * Mathf.Exp(-Mathf.Pow((t - 0.58f) / 0.13f, 2f));
            float rightBank = 12f * Mathf.Exp(-Mathf.Pow((t - 0.76f) / 0.05f, 2f));
            float leftShelf = 8f * Mathf.Exp(-Mathf.Pow((t - 0.18f) / 0.08f, 2f));

            int minHeight = Mathf.Max(6, Mathf.RoundToInt(worldHeight * 0.08f));
            int maxHeight = Mathf.Max(minHeight + 1, Mathf.RoundToInt(worldHeight * 0.68f));
            return Mathf.Clamp(Mathf.RoundToInt(descendingSlope + longWave + shortWave + centerBasin + rightBank + leftShelf), minHeight, maxHeight);
        }

        private void CarveArena(int[] surfaceHeights)
        {
            for (int x = 0; x < worldWidth; x++)
            {
                float t = worldWidth <= 1 ? 0f : x / (float)(worldWidth - 1);
                int carveDepth = Mathf.RoundToInt(Mathf.Lerp(3f, 1f, t));

                if (t > 0.42f && t < 0.68f)
                {
                    carveDepth += 3;
                }

                for (int y = surfaceHeights[x] - carveDepth; y < surfaceHeights[x] + 18; y++)
                {
                    if (grid.InBounds(x, y))
                    {
                        grid.SetMaterial(x, y, MaterialType.Air);
                    }
                }
            }

            CarveRoom(worldWidth * 3 / 5, worldHeight / 2, 32, 18);
        }

        private void CarveRoom(int centerX, int centerY, int radiusX, int radiusY)
        {
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    float dx = (x - centerX) / (float)Mathf.Max(1, radiusX);
                    float dy = (y - centerY) / (float)Mathf.Max(1, radiusY);
                    if (dx * dx + dy * dy <= 1f)
                    {
                        grid.SetMaterial(x, y, MaterialType.Air);
                    }
                }
            }
        }

        private void CarveBottomlessHole()
        {
            int centerX = bottomlessHoleCenterX < 0 ? worldWidth / 2 : bottomlessHoleCenterX;
            int safeWidth = Mathf.Max(1, bottomlessHoleWidth);
            int left = Mathf.Clamp(centerX - safeWidth / 2, 0, worldWidth - 1);
            int right = Mathf.Clamp(left + safeWidth - 1, 0, worldWidth - 1);

            for (int x = left; x <= right; x++)
            {
                for (int y = 0; y < worldHeight; y++)
                {
                    grid.SetMaterial(x, y, MaterialType.Air);
                }
            }

            int bevelHeight = Mathf.Max(3, safeWidth / 2);
            for (int i = 1; i <= bevelHeight; i++)
            {
                ClearColumnBand(left - i, bevelHeight - i + 1);
                ClearColumnBand(right + i, bevelHeight - i + 1);
            }
        }

        private void ClearColumnBand(int x, int height)
        {
            if (!grid.InBounds(x, 0))
            {
                return;
            }

            int safeHeight = Mathf.Clamp(height, 0, worldHeight);
            for (int y = 0; y < safeHeight; y++)
            {
                grid.SetMaterial(x, y, MaterialType.Air);
            }
        }

        private void SeedWater(int[] surfaceHeights)
        {
            int reservoirStart = Mathf.Max(2, worldWidth / 14);
            int reservoirEnd = Mathf.Min(worldWidth - 3, worldWidth / 4);

            for (int x = reservoirStart; x <= reservoirEnd; x++)
            {
                int top = Mathf.Min(worldHeight - 4, surfaceHeights[x] + 22);
                int bottom = Mathf.Min(worldHeight - 5, surfaceHeights[x] + 5);

                for (int y = bottom; y <= top; y++)
                {
                    grid.SetMaterial(x, y, MaterialType.Water);
                }
            }
        }

        private void SeedSand(int[] surfaceHeights)
        {
            int center = Mathf.RoundToInt(worldWidth * 0.36f);
            for (int x = center - 14; x <= center + 14; x++)
            {
                if (x < 0 || x >= worldWidth)
                {
                    continue;
                }

                int pileHeight = Mathf.RoundToInt(15f * Mathf.Clamp01(1f - Mathf.Abs(x - center) / 14f));
                for (int y = surfaceHeights[x] + 1; y <= surfaceHeights[x] + pileHeight; y++)
                {
                    grid.SetMaterial(x, y, MaterialType.Sand);
                }
            }
        }

        private void BuildWoodenChainReaction(int[] surfaceHeights)
        {
            int startX = Mathf.RoundToInt(worldWidth * 0.62f);
            int baseY = Mathf.Min(worldHeight - 20, surfaceHeights[startX] + 3);

            for (int x = startX; x < startX + 38 && x < worldWidth - 2; x++)
            {
                grid.SetMaterial(x, baseY, MaterialType.Wood);
                if (x % 6 == 0)
                {
                    for (int y = baseY - 9; y <= baseY; y++)
                    {
                        grid.SetMaterial(x, y, MaterialType.Wood);
                    }
                }
            }

            for (int y = baseY + 1; y < baseY + 12 && y < worldHeight - 2; y++)
            {
                grid.SetMaterial(startX + 34, y, MaterialType.Wood);
            }

            grid.PaintCircle(startX + 2, baseY + 2, 2, MaterialType.Fire);
        }

        private void SeedSmokePocket(int[] surfaceHeights)
        {
            int centerX = Mathf.RoundToInt(worldWidth * 0.62f);
            int centerY = Mathf.Min(worldHeight - 8, surfaceHeights[centerX] + 30);

            for (int x = centerX - 8; x <= centerX + 8; x++)
            {
                for (int y = centerY - 5; y <= centerY + 5; y++)
                {
                    if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) < 52)
                    {
                        grid.SetMaterial(x, y, MaterialType.Smoke);
                    }
                }
            }
        }
    }
}
