using System;
using UnityEngine;

namespace NoitaCA
{
    [Obsolete("Use Pixel instead. PixelCell is kept only for older scene scripts.")]
    public struct PixelCell
    {
        public MaterialType MaterialType;
        public Color32 Color;
        public bool UpdatedThisFrame;
        public int FallingFrames;
        public int Lifetime;
        public sbyte VelocityX;
        public sbyte VelocityY;

        public PixelCell(MaterialType materialType, Color32 color)
        {
            MaterialType = materialType;
            Color = color;
            UpdatedThisFrame = false;
            FallingFrames = 0;
            Lifetime = 0;
            VelocityX = 0;
            VelocityY = 0;
        }

        public static PixelCell FromMaterial(MaterialType materialType)
        {
            MaterialDefinition definition = MaterialDatabase.Get(materialType);
            return new PixelCell(definition.Type, definition.Color);
        }
    }
}
