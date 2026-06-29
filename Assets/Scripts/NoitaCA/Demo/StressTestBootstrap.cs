using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NoitaCA
{
    [DefaultExecutionOrder(-90)]
    public sealed class StressTestBootstrap : MonoBehaviour
    {
        [SerializeField] private StressTestConfig config = new StressTestConfig();

        private readonly List<Vector2Int> gateCells = new List<Vector2Int>(256);
        private readonly Stopwatch renderStopwatch = new Stopwatch();
        private PixelGrid grid;
        private PixelSimulation simulation;
        private PixelWorldRenderer worldRenderer;
        private SimplePixelPlayer player;
        private StressTestPerformancePanel performancePanel;
        private StressTestDebugOverlay debugOverlay;
        private Camera targetCamera;
        private float simulationAccumulator;
        private float elapsedSinceBuild;
        private float countAccumulator;
        private bool gateOpened;
        private Vector2Int playerSpawnCell;
        private RectInt tankInterior;

        public StressTestConfig Config => config;
        public PixelGrid Grid => grid;
        public PixelWorldRenderer Renderer => worldRenderer;
        public PixelSimulationStats Stats => simulation != null ? simulation.LastStats : default(PixelSimulationStats);
        public PixelSimulationMode Mode => simulation != null ? simulation.Mode : PixelSimulationMode.FullScan;
        public bool GateOpened => gateOpened;

        private void Awake()
        {
            BuildStressTestWorld();
        }

        private void Update()
        {
            if (grid == null || simulation == null || worldRenderer == null)
            {
                return;
            }

            HandleHotkeys();
            elapsedSinceBuild += Time.deltaTime;
            if (!gateOpened && config.autoStart && elapsedSinceBuild >= Mathf.Max(0f, config.gateOpenDelay))
            {
                OpenGate();
            }

            Simulate();
            RenderAndRecordTime();
            RefreshMaterialCounts();
        }

        public void BuildStressTestWorld()
        {
            int width = Mathf.Max(64, config.gridWidth);
            int height = Mathf.Max(64, config.gridHeight);
            grid = new PixelGrid(width, height);
            grid.ConfigureOptimization(config.chunkSize, config.chunkSleepDelay);

            simulation = new PixelSimulation();
            ApplyMode(config.initialMode);
            simulation.MaxProcessedPixelsPerStep = Mathf.Max(0, config.maxSimulatedPixelsPerStep);

            BuildTerrain();
            BuildWaterTank();
            BuildWoodenStructures();
            grid.ActivateAll();

            targetCamera = GetOrCreateCamera();
            worldRenderer = GetOrCreateRenderer();
            ConfigureDisplayTransform();
            worldRenderer.Initialize(grid, config.pixelsPerUnit);
            ConfigureCamera();
            player = GetOrCreatePlayer();
            player.Initialize(grid, worldRenderer, playerSpawnCell);
            performancePanel = GetOrCreatePerformancePanel();
            performancePanel.Initialize(this);
            debugOverlay = GetOrCreateDebugOverlay();
            debugOverlay.Initialize(this, targetCamera);

            gateOpened = false;
            elapsedSinceBuild = 0f;
            simulationAccumulator = 0f;
            countAccumulator = 0f;
            RenderAndRecordTime();
            RefreshMaterialCounts(true);
        }

        public void OpenGate()
        {
            if (gateOpened)
            {
                return;
            }

            gateOpened = true;
            for (int i = 0; i < gateCells.Count; i++)
            {
                Vector2Int cell = gateCells[i];
                grid.SetMaterial(cell.x, cell.y, MaterialType.Air);
            }

            // Demo simplification: the gate is instant; a production game might animate and wake a wider dirty region.
            if (gateCells.Count > 0)
            {
                Vector2Int first = gateCells[0];
                grid.MarkActiveArea(first.x, first.y, Mathf.Max(8, grid.ChunkSize));
            }
        }

        public void InjectWaterFromTop()
        {
            if (grid == null)
            {
                return;
            }

            int left = Mathf.Clamp(tankInterior.xMin, 1, grid.Width - 2);
            int right = Mathf.Clamp(tankInterior.xMax, left + 1, grid.Width - 1);
            int top = Mathf.Clamp(grid.Height - 6, 1, grid.Height - 2);
            int bottom = Mathf.Max(1, top - 5);

            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x < right; x++)
                {
                    grid.SetMaterial(x, y, MaterialType.Water);
                }
            }
        }

        public void ApplyMode(PixelSimulationMode mode)
        {
            if (simulation == null)
            {
                return;
            }

            if (mode == PixelSimulationMode.ActivePixels && !config.enableActiveRegionOptimization)
            {
                mode = PixelSimulationMode.FullScan;
            }
            else if (mode == PixelSimulationMode.ChunkBased && !config.enableChunkUpdates)
            {
                mode = PixelSimulationMode.FullScan;
            }

            simulation.Mode = mode;
            if (grid != null)
            {
                grid.ActivateAll();
            }
        }

        private void Simulate()
        {
            simulation.MaxProcessedPixelsPerStep = Mathf.Max(0, config.maxSimulatedPixelsPerStep);
            simulationAccumulator += Time.deltaTime;
            float interval = Mathf.Max(0.001f, config.simulationStepInterval);
            int ticks = 0;

            while (simulationAccumulator >= interval && ticks < 8)
            {
                int steps = Mathf.Max(1, config.simulationStepsPerFrame);
                for (int i = 0; i < steps; i++)
                {
                    simulation.Step(grid);
                }

                simulationAccumulator -= interval;
                ticks++;
            }

            if (ticks >= 8)
            {
                simulationAccumulator = 0f;
            }
        }

        private void RenderAndRecordTime()
        {
            renderStopwatch.Reset();
            renderStopwatch.Start();
            worldRenderer.Render();
            renderStopwatch.Stop();
            simulation.SetRenderTime((float)renderStopwatch.Elapsed.TotalMilliseconds);
        }

        private void RefreshMaterialCounts(bool force = false)
        {
            countAccumulator += Time.deltaTime;
            if (!force && countAccumulator < 0.2f)
            {
                return;
            }

            countAccumulator = 0f;
            grid.CountMaterials(out int nonAirPixels, out int waterPixels);
            simulation.SetMaterialCounts(nonAirPixels, waterPixels);
        }

        private void HandleHotkeys()
        {
            if (WasKeyPressed(KeyCode.Space))
            {
                OpenGate();
            }

            if (WasKeyPressed(KeyCode.R))
            {
                BuildStressTestWorld();
                return;
            }

            if (WasKeyPressed(KeyCode.Alpha1))
            {
                ApplyMode(PixelSimulationMode.FullScan);
            }
            else if (WasKeyPressed(KeyCode.Alpha2))
            {
                ApplyMode(PixelSimulationMode.ActivePixels);
            }
            else if (WasKeyPressed(KeyCode.Alpha3))
            {
                ApplyMode(PixelSimulationMode.ChunkBased);
            }

            if (WasKeyPressed(KeyCode.F1))
            {
                config.showPerformancePanel = !config.showPerformancePanel;
            }

            if (WasKeyPressed(KeyCode.F2))
            {
                config.showChunkBoundaries = !config.showChunkBoundaries;
            }

            if (WasKeyPressed(KeyCode.F3))
            {
                config.showActiveRegions = !config.showActiveRegions;
            }

            if (IsKeyHeld(KeyCode.W))
            {
                InjectWaterFromTop();
            }
        }

        private void BuildTerrain()
        {
            int width = grid.Width;
            int height = grid.Height;

            for (int x = 0; x < width; x++)
            {
                int groundHeight = GetStressTerrainHeight(x, width, height);
                for (int y = 0; y <= groundHeight; y++)
                {
                    grid.SetMaterial(x, y, MaterialType.Stone);
                }
            }

            BuildStoneRect(42, 58, 82, 5);
            BuildStoneRect(132, 42, 68, 5);
            BuildStoneRect(226, 70, 96, 5);
            BuildStoneRect(292, 35, 52, 5);

            BuildStoneSlope(48, 35, 112, 62, 4);
            BuildStoneSlope(182, 28, 248, 58, 5);
            BuildStoneSlope(310, 74, 358, 42, 4);

            CarveAirRect(width / 2 - 24, 1, 18, 44);
            CarveAirRect(width / 2 + 42, 1, 26, 24);
            CarveAirRect(width - 58, 1, 30, 70);
            CarveAirRect(96, 30, 16, 36);
            CarveAirRect(204, 34, 18, 32);

            BuildStoneRect(width / 2 - 28, 45, 26, 4);
            BuildStoneRect(width / 2 + 28, 28, 40, 4);

            playerSpawnCell = new Vector2Int(Mathf.Clamp(38, 4, width - 5), Mathf.Clamp(GetStressTerrainHeight(38, width, height) + 14, 12, height - 12));
        }

        private void BuildWaterTank()
        {
            gateCells.Clear();

            int tankLeft = 8;
            int waterWidth = Mathf.Clamp(config.initialWaterWidth, 16, grid.Width - 48);
            int waterHeight = Mathf.Clamp(config.initialWaterHeight, 16, grid.Height - 48);
            int tankBottom = Mathf.Clamp(grid.Height - waterHeight - 22, 24, grid.Height - 24);
            int tankRight = Mathf.Min(grid.Width - 8, tankLeft + waterWidth + 3);
            int tankTop = Mathf.Min(grid.Height - 4, tankBottom + waterHeight + 3);
            tankInterior = new RectInt(tankLeft + 2, tankBottom + 2, tankRight - tankLeft - 4, tankTop - tankBottom - 4);

            BuildStoneRect(tankLeft, tankBottom, tankRight - tankLeft + 1, 2);
            BuildStoneRect(tankLeft, tankBottom, 2, tankTop - tankBottom + 1);
            BuildStoneRect(tankRight - 1, tankBottom, 2, tankTop - tankBottom + 1);

            int gateHeight = Mathf.Clamp(waterHeight / 3, 12, 32);
            for (int y = tankBottom + 2; y < tankBottom + 2 + gateHeight; y++)
            {
                gateCells.Add(new Vector2Int(tankRight - 1, y));
                gateCells.Add(new Vector2Int(tankRight, y));
            }

            for (int y = tankInterior.yMin; y < tankInterior.yMax; y++)
            {
                for (int x = tankInterior.xMin; x < tankInterior.xMax; x++)
                {
                    grid.SetMaterial(x, y, MaterialType.Water);
                }
            }
        }

        private void BuildWoodenStructures()
        {
            BuildWoodRect(168, 76, 48, 4);
            BuildWoodRect(168, 54, 4, 24);
            BuildWoodRect(212, 54, 4, 24);
            BuildWoodRect(266, 82, 54, 4);
            BuildWoodRect(292, 54, 4, 30);

            grid.PaintCircle(169, 80, 2, MaterialType.Fire);
        }

        private int GetStressTerrainHeight(int x, int width, int height)
        {
            float t = width <= 1 ? 0f : x / (float)(width - 1);
            float baseHeight = Mathf.Lerp(height * 0.18f, height * 0.12f, t);
            float wave = Mathf.Sin(t * Mathf.PI * 5.5f) * 7f;
            float basin = -14f * Mathf.Exp(-Mathf.Pow((t - 0.55f) / 0.16f, 2f));
            float bank = 18f * Mathf.Exp(-Mathf.Pow((t - 0.78f) / 0.07f, 2f));
            return Mathf.Clamp(Mathf.RoundToInt(baseHeight + wave + basin + bank), 6, Mathf.RoundToInt(height * 0.42f));
        }

        private void BuildStoneRect(int x, int y, int width, int height)
        {
            FillRect(x, y, width, height, MaterialType.Stone);
        }

        private void BuildWoodRect(int x, int y, int width, int height)
        {
            FillRect(x, y, width, height, MaterialType.Wood);
        }

        private void CarveAirRect(int x, int y, int width, int height)
        {
            FillRect(x, y, width, height, MaterialType.Air);
        }

        private void FillRect(int x, int y, int width, int height, MaterialType material)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    grid.SetMaterial(px, py, material);
                }
            }
        }

        private void BuildStoneSlope(int startX, int startY, int endX, int endY, int thickness)
        {
            int steps = Mathf.Max(1, Mathf.Abs(endX - startX));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(startX, endX, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(startY, endY, t));
                FillRect(x, y, thickness, thickness, MaterialType.Stone);
            }
        }

        private PixelWorldRenderer GetOrCreateRenderer()
        {
            Transform existingDisplay = transform.Find("Stress Test Pixel World");
            GameObject displayObject = existingDisplay == null
                ? new GameObject("Stress Test Pixel World")
                : existingDisplay.gameObject;
            displayObject.transform.SetParent(transform, false);

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

        private SimplePixelPlayer GetOrCreatePlayer()
        {
            Transform existingPlayer = transform.Find("Stress Test Player");
            GameObject playerObject = existingPlayer == null
                ? new GameObject("Stress Test Player")
                : existingPlayer.gameObject;
            playerObject.transform.SetParent(transform, false);

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

        private StressTestPerformancePanel GetOrCreatePerformancePanel()
        {
            if (!TryGetComponent(out StressTestPerformancePanel panel))
            {
                panel = gameObject.AddComponent<StressTestPerformancePanel>();
            }

            return panel;
        }

        private StressTestDebugOverlay GetOrCreateDebugOverlay()
        {
            if (!TryGetComponent(out StressTestDebugOverlay overlay))
            {
                overlay = gameObject.AddComponent<StressTestDebugOverlay>();
            }

            return overlay;
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
            Vector2 worldSize = new Vector2(grid.Width / (float)config.pixelsPerUnit, grid.Height / (float)config.pixelsPerUnit);
            worldRenderer.transform.position = new Vector3(-worldSize.x * 0.5f, -worldSize.y * 0.5f, 0f);
            worldRenderer.transform.rotation = Quaternion.identity;
            worldRenderer.transform.localScale = Vector3.one;
        }

        private void ConfigureCamera()
        {
            Vector2 worldSize = new Vector2(grid.Width / (float)config.pixelsPerUnit, grid.Height / (float)config.pixelsPerUnit);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = worldSize.y * 0.5f + Mathf.Max(0f, config.cameraPadding);
            targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            targetCamera.transform.rotation = Quaternion.identity;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.012f, 0.014f, 0.019f, 1f);
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switch (keyCode)
                {
                    case KeyCode.Space:
                        return Keyboard.current.spaceKey.wasPressedThisFrame;
                    case KeyCode.R:
                        return Keyboard.current.rKey.wasPressedThisFrame;
                    case KeyCode.Alpha1:
                        return Keyboard.current.digit1Key.wasPressedThisFrame;
                    case KeyCode.Alpha2:
                        return Keyboard.current.digit2Key.wasPressedThisFrame;
                    case KeyCode.Alpha3:
                        return Keyboard.current.digit3Key.wasPressedThisFrame;
                    case KeyCode.F1:
                        return Keyboard.current.f1Key.wasPressedThisFrame;
                    case KeyCode.F2:
                        return Keyboard.current.f2Key.wasPressedThisFrame;
                    case KeyCode.F3:
                        return Keyboard.current.f3Key.wasPressedThisFrame;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        private static bool IsKeyHeld(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && keyCode == KeyCode.W)
            {
                return Keyboard.current.wKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(keyCode);
#else
            return false;
#endif
        }
    }
}
