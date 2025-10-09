

using UnityEngine;
using UnityEngine.UI;

namespace AutoUI
{
    // 这个类是一个对Pixel进行处理的元操作
    public class AutoUIPictureTool
    {
        public static void PictureLayerGameObjectAddSprite(GameObject gameObject, Sprite sprite, Layer layer)
        {
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            Color color = image.color;
            color.a = layer.opacity;
            image.color = color;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                // 这是九宫格
                image.type = Image.Type.Sliced;
            }
            // 统一 raycastTarget 策略
            if (AutoUIConfig.config != null && AutoUIConfig.config.Default != null && AutoUIConfig.config.Default.UIInteract != null)
            {
                image.raycastTarget = AutoUIConfig.config.Default.UIInteract.ImageRaycastTarget;
            }
        }
        
        
        public static void AddSpriteFromLayer(ref GameObject gameobject, in Layer layer)
        {
            FindSpriteResult result = AutoUIAssets.GetSprite(layer.name);
            if (result == null)
            {
                LogUtil.LogError("无法找到对应的sprite:" + layer.name);
                // 兜底：依然创建 Image，便于后续组件绑定
                PictureLayerGameObjectAddSprite(gameobject, null, layer);
                return;
            }
            switch (result.status)
            {
                case EFindAssetStatus.oneResult:
                    Sprite sprite = result.oneResult.sprite;
                    PictureLayerGameObjectAddSprite(gameobject, sprite, layer);
                    break;
                case EFindAssetStatus.manyResult:
                    LogUtil.LogWarning("出现了多个同名的sprite:" + layer.name + "需要手动解决，已使用空Image兜底");
                    PictureLayerGameObjectAddSprite(gameobject, null, layer);
                    break;
                case EFindAssetStatus.cantFind:
                    LogUtil.LogWarning("没有找到对应的sprite:" + layer.name + "，已使用空Image兜底");
                    PictureLayerGameObjectAddSprite(gameobject, null, layer);
                    break;
                default:
                    LogUtil.LogError("出现了无法解析的EFIndAssetStatus:" + result.status);
                    PictureLayerGameObjectAddSprite(gameobject, null, layer);
                    break;
            }
        }

    }
}