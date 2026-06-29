using System;
using UnityEngine;

namespace NoitaCA
{
    public enum PixelMovementMode
    {
        Static,
        Powder,
        Liquid,
        Gas
    }

    public readonly struct MaterialDefinition
    {
        public readonly MaterialType Type;
        public readonly string DisplayName;
        public readonly Color32 Color;
        public readonly int Density;
        public readonly float StartTemperature;
        public readonly int StartLifetime;
        public readonly PixelMovementMode MovementMode;
        public readonly int VerticalDirection;
        public readonly bool CanMoveVertical;
        public readonly bool CanMoveDiagonal;
        public readonly bool CanMoveHorizontal;
        public readonly int HorizontalSearchDistance;
        public readonly float MoveProbability;
        public readonly float LateralProbability;
        public readonly bool CanBeDisplaced;
        public readonly bool BlocksPlayer;
        public readonly bool ConsumesLifetime;
        public readonly int LifetimeDecay;
        public readonly float HeatEmission;
        public readonly float IgniteTemperature;
        public readonly float Flammability;
        public readonly MaterialType BurnMaterial;
        public readonly MaterialType BurnoutMaterial;
        public readonly MaterialType AlternateBurnoutMaterial;
        public readonly float AlternateBurnoutChance;
        public readonly int BurnLifetimeMin;
        public readonly int BurnLifetimeMax;

        public MaterialDefinition(
            MaterialType type,
            string displayName,
            Color32 color,
            int density,
            float startTemperature,
            int startLifetime,
            PixelMovementMode movementMode,
            int verticalDirection,
            bool canMoveVertical,
            bool canMoveDiagonal,
            bool canMoveHorizontal,
            int horizontalSearchDistance,
            float moveProbability,
            float lateralProbability,
            bool canBeDisplaced,
            bool blocksPlayer,
            bool consumesLifetime,
            int lifetimeDecay,
            float heatEmission,
            float igniteTemperature,
            float flammability,
            MaterialType burnMaterial,
            MaterialType burnoutMaterial,
            MaterialType alternateBurnoutMaterial,
            float alternateBurnoutChance,
            int burnLifetimeMin,
            int burnLifetimeMax)
        {
            Type = type;
            DisplayName = displayName;
            Color = color;
            Density = density;
            StartTemperature = startTemperature;
            StartLifetime = startLifetime;
            MovementMode = movementMode;
            VerticalDirection = Math.Sign(verticalDirection);
            CanMoveVertical = canMoveVertical;
            CanMoveDiagonal = canMoveDiagonal;
            CanMoveHorizontal = canMoveHorizontal;
            HorizontalSearchDistance = Mathf.Max(1, horizontalSearchDistance);
            MoveProbability = Mathf.Clamp01(moveProbability);
            LateralProbability = Mathf.Clamp01(lateralProbability);
            CanBeDisplaced = canBeDisplaced;
            BlocksPlayer = blocksPlayer;
            ConsumesLifetime = consumesLifetime;
            LifetimeDecay = Mathf.Max(1, lifetimeDecay);
            HeatEmission = heatEmission;
            IgniteTemperature = igniteTemperature;
            Flammability = Mathf.Clamp01(flammability);
            BurnMaterial = burnMaterial;
            BurnoutMaterial = burnoutMaterial;
            AlternateBurnoutMaterial = alternateBurnoutMaterial;
            AlternateBurnoutChance = Mathf.Clamp01(alternateBurnoutChance);
            BurnLifetimeMin = Mathf.Max(1, burnLifetimeMin);
            BurnLifetimeMax = Mathf.Max(BurnLifetimeMin, burnLifetimeMax);
        }

        public bool IsAir => Type == MaterialType.Air;
        public bool IsFlammable => Flammability > 0f;
    }

    public static class MaterialDatabase
    {
        private const float AmbientTemperature = 20f;
        private static readonly MaterialDefinition[] Definitions = BuildDefinitions();

        public static float Ambient => AmbientTemperature;

        public static MaterialDefinition Get(MaterialType type)
        {
            int index = (int)type;
            if (index < 0 || index >= Definitions.Length)
            {
                return Definitions[(int)MaterialType.Air];
            }

            return Definitions[index];
        }

        public static Pixel CreatePixel(MaterialType type)
        {
            MaterialDefinition definition = Get(type);
            return new Pixel(
                definition.Type,
                definition.Density,
                definition.StartTemperature,
                definition.StartLifetime,
                definition.Color,
                false,
                0,
                0,
                0,
                definition.BurnoutMaterial);
        }

        public static Pixel CreateBurningPixel(MaterialDefinition source, MaterialDefinition fire, MaterialType decayMaterial, int lifetime)
        {
            return new Pixel(
                fire.Type,
                fire.Density,
                fire.StartTemperature,
                Mathf.Max(1, lifetime),
                fire.Color,
                true,
                0,
                0,
                0,
                decayMaterial == MaterialType.Air ? MaterialType.Air : decayMaterial);
        }

        private static MaterialDefinition[] BuildDefinitions()
        {
            MaterialDefinition[] definitions = new MaterialDefinition[8];

            definitions[(int)MaterialType.Air] = new MaterialDefinition(
                MaterialType.Air, "Air", new Color32(8, 10, 14, 255), 0, AmbientTemperature, 0,
                PixelMovementMode.Static, 0, false, false, false, 1, 0f, 0f,
                true, false, false, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            definitions[(int)MaterialType.Sand] = new MaterialDefinition(
                MaterialType.Sand, "Sand", new Color32(213, 183, 104, 255), 70, AmbientTemperature, 0,
                PixelMovementMode.Powder, -1, true, true, false, 1, 1f, 1f,
                false, true, false, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            definitions[(int)MaterialType.Water] = new MaterialDefinition(
                MaterialType.Water, "Water", new Color32(44, 134, 214, 255), 30, AmbientTemperature, 0,
                PixelMovementMode.Liquid, -1, true, true, true, 6, 1f, 0.78f,
                true, false, false, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            definitions[(int)MaterialType.Smoke] = new MaterialDefinition(
                MaterialType.Smoke, "Smoke", new Color32(104, 112, 116, 180), -10, 55f, 150,
                PixelMovementMode.Gas, 1, true, true, true, 3, 0.92f, 0.86f,
                true, false, true, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            definitions[(int)MaterialType.Fire] = new MaterialDefinition(
                MaterialType.Fire, "Fire", new Color32(255, 104, 28, 255), -20, 420f, 30,
                PixelMovementMode.Static, 0, false, false, false, 1, 0f, 0f,
                true, false, true, 1, 34f, 145f, 0f, MaterialType.Fire, MaterialType.Smoke,
                MaterialType.Air, 0f, 18, 42);

            definitions[(int)MaterialType.Stone] = new MaterialDefinition(
                MaterialType.Stone, "Stone", new Color32(92, 84, 74, 255), 100, AmbientTemperature, 0,
                PixelMovementMode.Static, 0, false, false, false, 1, 0f, 0f,
                false, true, false, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            definitions[(int)MaterialType.Wood] = new MaterialDefinition(
                MaterialType.Wood, "Wood", new Color32(126, 78, 38, 255), 82, AmbientTemperature, 0,
                PixelMovementMode.Static, 0, false, false, false, 1, 0f, 0f,
                false, true, false, 1, 0f, 125f, 0.64f, MaterialType.Fire, MaterialType.Ash,
                MaterialType.Smoke, 0.35f, 34, 70);

            definitions[(int)MaterialType.Ash] = new MaterialDefinition(
                MaterialType.Ash, "Ash", new Color32(78, 74, 68, 255), 18, AmbientTemperature, 0,
                PixelMovementMode.Powder, -1, true, true, false, 1, 0.75f, 0.55f,
                true, false, false, 1, 0f, 0f, 0f, MaterialType.Fire, MaterialType.Air,
                MaterialType.Air, 0f, 1, 1);

            return definitions;
        }
    }
}
