using UnityEngine;

namespace NoitaCA
{
    public struct Pixel
    {
        public MaterialType MaterialType;
        public int Density;
        public float Temperature;
        public int Lifetime;
        public Color32 Color;
        public bool UpdatedThisFrame;
        public int FallingFrames;
        public sbyte VelocityX;
        public sbyte VelocityY;
        public MaterialType DecayMaterial;

        public Pixel(
            MaterialType materialType,
            int density,
            float temperature,
            int lifetime,
            Color32 color,
            bool updatedThisFrame = false,
            int fallingFrames = 0,
            sbyte velocityX = 0,
            sbyte velocityY = 0,
            MaterialType decayMaterial = MaterialType.Air)
        {
            MaterialType = materialType;
            Density = density;
            Temperature = temperature;
            Lifetime = lifetime;
            Color = color;
            UpdatedThisFrame = updatedThisFrame;
            FallingFrames = fallingFrames;
            VelocityX = velocityX;
            VelocityY = velocityY;
            DecayMaterial = decayMaterial;
        }

        public static Pixel FromMaterial(MaterialType materialType)
        {
            return MaterialDatabase.CreatePixel(materialType);
        }
    }
}
