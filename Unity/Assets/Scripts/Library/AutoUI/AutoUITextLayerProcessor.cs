using TMPro;
using UnityEditor;
using UnityEngine;

namespace AutoUI
{
	public class AutoUITextLayerProcessor
	{
		public static void TextLayerProcessor(in Layer layer, ref GameObject textGameObject)
		{
			///////// 基础设置
			TextMeshProUGUI tmp = textGameObject.AddComponent<TextMeshProUGUI>();

			// 默认对齐方式
			tmp.alignment = TextAlignmentOptions.Center;
			// 文本
			tmp.text = layer.textLayerData.text;

			// 字体大小
			tmp.fontSize = Mathf.RoundToInt(CorrectSizeValue(layer.textLayerData.fontSize));

			// 字体资源
			TMP_FontAsset tmpFontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AutoUIConfig.config.FontAssets.Default.Path);
			if (tmpFontAsset == null)
			{
				LogUtil.LogError("找不到字体资源 路径为:" + AutoUIConfig.config.FontAssets.Default.Path);
				return;
			}
			tmp.font = tmpFontAsset;

			// 字体颜色（支持 alpha，如果没有则使用 1），叠乘 layer.opacity
			float r = layer.textLayerData.color.r / 255f;
			float g = layer.textLayerData.color.g / 255f;
			float b = layer.textLayerData.color.b / 255f;
			float a = Mathf.Clamp01(layer.opacity);
			tmp.color = new Color(r, g, b, a);

			// raycastTarget 策略
			if (AutoUIConfig.config != null && AutoUIConfig.config.Default != null && AutoUIConfig.config.Default.UIInteract != null)
			{
				tmp.raycastTarget = AutoUIConfig.config.Default.UIInteract.TextRaycastTarget;
			}

			// 描边支持
			if (layer.textLayerData.haveShadow)
			{
				Material presetMaterial = AssetDatabase.LoadAssetAtPath<Material>(AutoUIConfig.config.FontAssets.Default.MaterialPreset.Shadow.Path);
				if (presetMaterial == null)
				{
					LogUtil.LogError("找不到预设材质 路径为:" + AutoUIConfig.config.FontAssets.Default.MaterialPreset.Shadow.Path);
					return;
				}
				tmp.fontSharedMaterial = presetMaterial;
			}

			// 自动换行
			tmp.enableWordWrapping = layer.textLayerData.warp;
			if (layer.textLayerData.warp)
			{
				tmp.alignment = TextAlignmentOptions.TopLeft;
			}

			// 文本的旋转
			tmp.rectTransform.rotation = Quaternion.Euler(0, 0, layer.textLayerData.rotation);

			// 一般来说文本的旋转如果是0那么肯定就需要文本居中，此类文字基本都是以tips的形式出现
			if (layer.textLayerData.rotation != 0)
			{
				tmp.alignment = TextAlignmentOptions.Center;
			}

			// 文本对齐映射（水平：left/center/right；垂直依据 pivot.y 与是否换行推断）
			if (!string.IsNullOrEmpty(layer.textLayerData.textAlign))
			{
				string align = layer.textLayerData.textAlign.ToLower();
				bool wrap = layer.textLayerData.warp;
				float pvY = layer.rectTransform != null ? layer.rectTransform.pivot.y : 0.5f;
				bool top = pvY > 0.66f;
				bool bottom = pvY < 0.34f;
				// 若换行，优先顶部；否则居中；若显式 pivot 偏下则底部
				TextAlignmentOptions hor;
				switch (align)
				{
					case "left": hor = TextAlignmentOptions.Left; break;
					case "right": hor = TextAlignmentOptions.Right; break;
					default: hor = TextAlignmentOptions.Center; break;
				}
				TextAlignmentOptions vert = wrap || top ? TextAlignmentOptions.Top : (bottom ? TextAlignmentOptions.Bottom : TextAlignmentOptions.Midline);
				// 合成（TMP 没有直接的组合枚举，使用拓展组合项）
				if (vert == TextAlignmentOptions.Top)
				{
					if (hor == TextAlignmentOptions.Left) tmp.alignment = TextAlignmentOptions.TopLeft;
					else if (hor == TextAlignmentOptions.Right) tmp.alignment = TextAlignmentOptions.TopRight;
					else tmp.alignment = TextAlignmentOptions.Top;
				}
				else if (vert == TextAlignmentOptions.Bottom)
				{
					if (hor == TextAlignmentOptions.Left) tmp.alignment = TextAlignmentOptions.BottomLeft;
					else if (hor == TextAlignmentOptions.Right) tmp.alignment = TextAlignmentOptions.BottomRight;
					else tmp.alignment = TextAlignmentOptions.Bottom;
				}
				else
				{
					if (hor == TextAlignmentOptions.Left) tmp.alignment = TextAlignmentOptions.Left;
					else if (hor == TextAlignmentOptions.Right) tmp.alignment = TextAlignmentOptions.Right;
					else tmp.alignment = TextAlignmentOptions.Midline;
				}
			}

			// 溢出策略（缺省：截断），可通过后续扩展读取组件参数
			tmp.overflowMode = TextOverflowModes.Truncate;

			// 本地化组件支持 项目强制
			if (AutoUIConfig.config.Default.Localization.IsUseLocalization)
			{
			}

			// title组件支持
			if (AutoUIUtil.IsComponentExist(in layer, "title"))
			{
				tmpFontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AutoUIConfig.config.FontAssets.Title.Path);
				if (tmpFontAsset == null)
				{
					LogUtil.LogError("找不到字体资源 路径为:" + AutoUIConfig.config.FontAssets.Title.Path);
					return;
				}
				tmp.font = tmpFontAsset;
				// 描边支持
				if (layer.textLayerData.haveShadow)
				{
					Material presetMaterial = AssetDatabase.LoadAssetAtPath<Material>(AutoUIConfig.config.FontAssets.Title.MaterialPreset.Shadow.Path);
					if (presetMaterial == null)
					{
						LogUtil.LogError("找不到预设材质 路径为:" + AutoUIConfig.config.FontAssets.Title.MaterialPreset.Shadow.Path);
						return;
					}
					tmp.fontSharedMaterial = presetMaterial;
				}
			}

			///////// 刷新一次tmp组件
			tmp.ForceMeshUpdate();
			AssetDatabase.SaveAssets();
		}
		public static float CorrectSizeValue(float value)
		{
			if (AutoUIConfig.config.Default.Font.EnableCorrect)
			{
				value *= AutoUIConfig.config.Default.Font.CorrectValue;
			}
			return value;
		}
	}

}