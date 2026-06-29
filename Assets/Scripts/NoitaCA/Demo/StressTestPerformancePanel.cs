using UnityEngine;

namespace NoitaCA
{
    public sealed class StressTestPerformancePanel : MonoBehaviour
    {
        private StressTestBootstrap bootstrap;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle hintStyle;
        private float fps;
        private float fpsAccumulator;
        private int fpsFrames;
        private float fpsTimer;

        public void Initialize(StressTestBootstrap stressTestBootstrap)
        {
            bootstrap = stressTestBootstrap;
        }

        private void Update()
        {
            fpsAccumulator += Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= 0.25f)
            {
                fps = fpsFrames > 0 ? fpsAccumulator / fpsFrames : 0f;
                fpsAccumulator = 0f;
                fpsFrames = 0;
                fpsTimer = 0f;
            }
        }

        private void OnGUI()
        {
            if (bootstrap == null || bootstrap.Config == null || !bootstrap.Config.showPerformancePanel)
            {
                return;
            }

            EnsureStyles();
            PixelSimulationStats stats = bootstrap.Stats;
            Rect panelRect = new Rect(18f, 18f, 680f, bootstrap.Config.showActivePixelCount ? 392f : 344f);

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Noita Pixel Stress Test / Noita 像素压力测试", titleStyle);
            DrawLine("Mode", "当前模式", GetModeLabel(stats.Mode), new Color(0.35f, 0.9f, 1f, 1f));
            DrawLine("FPS", "当前帧率", fps.ToString("0.0"), GetFpsColor(fps));
            DrawLine("Total Pixels", "网格总像素", stats.TotalPixels.ToString(), new Color(0.88f, 0.92f, 0.96f, 1f));
            DrawLine("Non-Air Pixels", "非空气像素", stats.NonAirPixels.ToString(), new Color(0.95f, 0.86f, 0.5f, 1f));
            DrawLine("Water Pixels", "水体像素", stats.WaterPixels.ToString(), new Color(0.35f, 0.66f, 1f, 1f));

            if (bootstrap.Config.showActivePixelCount)
            {
                DrawLine("Active Pixels", "活跃像素", stats.ActivePixels.ToString(), new Color(0.48f, 1f, 0.55f, 1f));
                DrawLine("Active Chunks", "活跃区块", stats.ActiveChunks.ToString(), new Color(0.65f, 1f, 0.75f, 1f));
            }

            DrawLine("Simulation", "模拟耗时", stats.SimulationMs.ToString("0.000") + " ms", GetCostColor(stats.SimulationMs));
            DrawLine("Render", "渲染耗时", stats.RenderMs.ToString("0.000") + " ms", GetCostColor(stats.RenderMs));
            DrawLine("Processed / Step", "每步处理像素", stats.ProcessedPixels.ToString(), new Color(1f, 0.72f, 0.38f, 1f));
            DrawLine("Chunk Optimized", "区块优化", stats.UseChunkOptimization ? "ON / 开启" : "OFF / 关闭", stats.UseChunkOptimization ? new Color(0.52f, 1f, 0.62f, 1f) : new Color(1f, 0.48f, 0.42f, 1f));
            DrawLine("Active Region", "活跃区域优化", stats.UseActiveRegionOptimization ? "ON / 开启" : "OFF / 关闭", stats.UseActiveRegionOptimization ? new Color(0.52f, 1f, 0.62f, 1f) : new Color(1f, 0.48f, 0.42f, 1f));
            DrawLine("Gate", "水闸状态", bootstrap.GateOpened ? "OPEN / 已打开" : "CLOSED / 已关闭", bootstrap.GateOpened ? new Color(0.48f, 1f, 0.74f, 1f) : new Color(1f, 0.64f, 0.28f, 1f));
            GUILayout.Space(6f);
            GUILayout.Label("Space 倒水 | R 重置 | 1/2/3 切模式 | F1 面板 | F2 区块 | F3 活跃区 | W 加水", hintStyle);
            GUILayout.EndArea();
        }

        private void DrawLine(string englishLabel, string chineseLabel, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(englishLabel, labelStyle, GUILayout.Width(180f));
            GUILayout.Label(chineseLabel, labelStyle, GUILayout.Width(180f));
            valueStyle.normal.textColor = valueColor;
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        private static string GetModeLabel(PixelSimulationMode mode)
        {
            switch (mode)
            {
                case PixelSimulationMode.ActivePixels:
                    return "Active Pixels / 活跃像素";
                case PixelSimulationMode.ChunkBased:
                    return "Chunk Based / 区块更新";
                case PixelSimulationMode.FullScan:
                default:
                    return "Full Scan / 全图扫描";
            }
        }

        private static Color GetFpsColor(float currentFps)
        {
            if (currentFps >= 50f)
            {
                return new Color(0.42f, 1f, 0.52f, 1f);
            }

            if (currentFps >= 25f)
            {
                return new Color(1f, 0.86f, 0.25f, 1f);
            }

            return new Color(1f, 0.32f, 0.28f, 1f);
        }

        private static Color GetCostColor(float milliseconds)
        {
            if (milliseconds <= 4f)
            {
                return new Color(0.42f, 1f, 0.52f, 1f);
            }

            if (milliseconds <= 12f)
            {
                return new Color(1f, 0.86f, 0.25f, 1f);
            }

            return new Color(1f, 0.42f, 0.28f, 1f);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.98f, 1f, 1f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                normal = { textColor = new Color(0.9f, 0.94f, 0.96f, 1f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = new Color(0.72f, 0.82f, 0.88f, 1f) }
            };
        }
    }
}
