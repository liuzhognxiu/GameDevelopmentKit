using UnityEngine;

namespace AutoUI
{
    public class AutoUISmartObjectLayerProcessor
    {
        public static void SmartObjectLayerProcessor(in Layer layer, ref GameObject smartObjectGameObject)
        {
            AutoUIPictureTool.AddSpriteFromLayer(ref smartObjectGameObject,in layer);
        }
            
    }
}