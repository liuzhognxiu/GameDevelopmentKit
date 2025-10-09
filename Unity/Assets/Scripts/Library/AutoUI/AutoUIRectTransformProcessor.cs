using UnityEngine;


namespace AutoUI
{


	class AutoUIRectTransformProcessor
	{

		public static void RectTransformProcessor(ref UnityEngine.RectTransform rectTransform, in LayerRectTransform layerRectTransformData)
		{
			rectTransform.anchoredPosition = layerRectTransformData.anchoredPosition.ToVector2();
			rectTransform.sizeDelta = layerRectTransformData.sizeDelta.ToVector2();
			if (layerRectTransformData.anchor == null)
			{
				LogUtil.LogError("layerRectTransformData.anchor is null,请检查JSON搜索null,非常非常有可能是美术在图层命名的时候使用到/符号导致");
			}
			rectTransform.anchorMin = layerRectTransformData.anchor[0].ToVector2();
			rectTransform.anchorMax = layerRectTransformData.anchor[1].ToVector2();
			rectTransform.pivot = layerRectTransformData.pivot.ToVector2();

			// Stretch 支持：当某轴 anchorMin != anchorMax 时，优先使用 offsetMin/offsetMax，并将该轴的 sizeDelta 置 0
			Vector2 anchorMin = rectTransform.anchorMin;
			Vector2 anchorMax = rectTransform.anchorMax;
			bool stretchX = !Mathf.Approximately(anchorMin.x, anchorMax.x);
			bool stretchY = !Mathf.Approximately(anchorMin.y, anchorMax.y);

			Vector2 sizeDelta = layerRectTransformData.sizeDelta.ToVector2();
			Vector2 anchored = layerRectTransformData.anchoredPosition.ToVector2();
			if (stretchX || stretchY)
			{
				Vector2 offMin = layerRectTransformData.offsetMin.ToVector2();
				Vector2 offMax = layerRectTransformData.offsetMax.ToVector2();
				rectTransform.offsetMin = offMin;
				rectTransform.offsetMax = offMax;
				if (stretchX) { sizeDelta.x = 0f; anchored.x = 0f; }
				if (stretchY) { sizeDelta.y = 0f; anchored.y = 0f; }
			}
			rectTransform.sizeDelta = sizeDelta;
			rectTransform.anchoredPosition = anchored;
			rectTransform.localScale = Vector3.one;
		}
	}

}
