using UnityEngine;

namespace AutoUI
{
    public class AutoUIPixelLayerProcessor
    {
        public static void PixelLayerProcessor(in Layer layer, ref GameObject pixelGameObject)
        {
            AutoUIPictureTool.AddSpriteFromLayer(ref pixelGameObject, in layer);
        }
    }

}