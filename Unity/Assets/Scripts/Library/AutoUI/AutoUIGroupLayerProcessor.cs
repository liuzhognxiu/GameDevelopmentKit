using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AutoUI
{
    class AutoUIGroupLayerProcessor
    {
        public static List<string> ExistPrefabNames = new List<string>();

        public static void ClearExistPrefabNames()
        {
            ExistPrefabNames.Clear();
        }

        public static void GroupLayerProcessor(in Layer layer, ref GameObject newGameObject)
        {
            // 基本处理
            // 组层透明度：使用 CanvasGroup 表达整体 alpha，不影响交互
            var canvasGroup = newGameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = newGameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = layer.opacity;

            if (AutoUIUtil.IsComponentExist(in layer, "ExButton"))
            {
                var button = newGameObject.AddComponent<Game.ExButton>();
                if (AutoUIConfig.config.Default.ButtonClickEffect.EnableClickEffect)
                {
                    // 如果使用点击效果
                }
            }

            /////// 处理组件
            if (AutoUIUtil.IsComponentExist(in layer, "button"))
            {
                var button = newGameObject.GetComponent<UnityEngine.UI.Button>();
                if (button == null)
                {
                    button = newGameObject.AddComponent<UnityEngine.UI.Button>();
                }

                // 寻找合适的 targetGraphic
                Image TryFindImage(Transform root)
                {
                    var img = root.GetComponent<Image>();
                    if (img != null) return img;
                    // 按关键词猜测常见背景命名
                    for (int i = 0; i < root.childCount; i++)
                    {
                        var t = root.GetChild(i);
                        string lower = t.name.ToLower();
                        if ((lower.Contains("background") || lower.Contains("bg") || lower.Contains("image")) &&
                            t.GetComponent<Image>() != null)
                        {
                            return t.GetComponent<Image>();
                        }
                    }

                    return null;
                }

                var target = TryFindImage(newGameObject.transform);
                if (target == null)
                {
                    // 兜底：自身挂一个空 Image 作为 target
                    target = newGameObject.AddComponent<Image>();
                }

                button.targetGraphic = target;
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
                colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
                button.colors = colors;
                if (AutoUIConfig.config.Default.ButtonClickEffect.EnableClickEffect)
                {
                    // 如果使用点击效果
                }
            }

            // 处理layout
            if (AutoUIUtil.IsComponentExist(in layer, "grid"))
            {
                newGameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                // 自动推导gridLayout的参数
                AutoUILayoutProcessor.GridLayout参数自动推导(in layer, ref newGameObject);
            }

            if (AutoUIUtil.IsComponentExist(in layer, "horizontalLayout"))
            {
                newGameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                // 自动推导horizontalLayout的参数
                AutoUILayoutProcessor.ApplyHorizontalLayout(in layer, ref newGameObject);
            }

            if (AutoUIUtil.IsComponentExist(in layer, "verticalLayout"))
            {
                newGameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                // 自动推导verticalLayout的参数
                AutoUILayoutProcessor.ApplyVerticalLayout(in layer, ref newGameObject);
            }

            // 处理 slider/progress（进度条/滑动条）基础组件挂载
            if (AutoUIUtil.IsComponentExist(in layer, "slider") || AutoUIUtil.IsComponentExist(in layer, "progress"))
            {
                var slider = newGameObject.GetComponent<UnityEngine.UI.Slider>();
                if (slider == null)
                {
                    slider = newGameObject.AddComponent<UnityEngine.UI.Slider>();
                }

                // 默认从左到右
                slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
                // progress 作为不可交互进度条
                bool isProgress = AutoUIUtil.IsComponentExist(in layer, "progress");
                if (isProgress)
                {
                    slider.interactable = false;
                }
            }

            // 处理 toggle（开关/勾选）基础组件挂载
            if (AutoUIUtil.IsComponentExist(in layer, "toggle"))
            {
                var toggle = newGameObject.GetComponent<UnityEngine.UI.Toggle>();
                if (toggle == null)
                {
                    toggle = newGameObject.AddComponent<UnityEngine.UI.Toggle>();
                }
            }

            // 处理 scroll（滚动）组件装配
            if (AutoUIUtil.IsComponentExist(in layer, "scroll"))
            {
                try
                {
                    var scroll = newGameObject.GetComponent<UnityEngine.UI.ScrollRect>();
                    if (scroll == null) scroll = newGameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                    string viewportName = null, contentName = null, direction = "vertical";
                    UnityEngine.UI.ScrollRect.MovementType movement = UnityEngine.UI.ScrollRect.MovementType.Clamped;
                    bool inertia = true;
                    float deceleration = 0.135f, elasticity = 0.1f;
                    if (layer.components != null)
                    {
                        foreach (var c in layer.components)
                        {
                            if (c.name == "scroll")
                            {
                                object o;
                                if (c.parameters != null)
                                {
                                    if (c.parameters.TryGetValue("viewport", out o)) viewportName = o as string;
                                    if (c.parameters.TryGetValue("content", out o)) contentName = o as string;
                                    if (c.parameters.TryGetValue("direction", out o) && o is string)
                                        direction = ((string)o).ToLower();
                                    if (c.parameters.TryGetValue("movementType", out o) && o is string)
                                        System.Enum.TryParse((string)o, true, out movement);
                                    if (c.parameters.TryGetValue("inertia", out o))
                                        inertia = System.Convert.ToBoolean(o);
                                    if (c.parameters.TryGetValue("decelerationRate", out o))
                                        deceleration = System.Convert.ToSingle(o);
                                    if (c.parameters.TryGetValue("elasticity", out o))
                                        elasticity = System.Convert.ToSingle(o);
                                }

                                break;
                            }
                        }
                    }

                    // 构建/查找 Viewport
                    Transform FindDeep(Transform root, string name)
                    {
                        if (root == null || string.IsNullOrEmpty(name)) return null;
                        if (root.name == name) return root;
                        for (int i = 0; i < root.childCount; i++)
                        {
                            var r = FindDeep(root.GetChild(i), name);
                            if (r != null) return r;
                        }

                        return null;
                    }

                    Transform viewport = null;
                    if (!string.IsNullOrEmpty(viewportName)) viewport = FindDeep(newGameObject.transform, viewportName);
                    if (viewport == null)
                    {
                        var go = new GameObject("Viewport", typeof(UnityEngine.RectTransform), typeof(Mask));
                        go.transform.SetParent(newGameObject.transform, false);
                        var rt = go.GetComponent<UnityEngine.RectTransform>();
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        go.GetComponent<Mask>().showMaskGraphic = false;
                        viewport = go.transform;
                    }

                    // 构建/查找 Content
                    Transform content = null;
                    if (!string.IsNullOrEmpty(contentName)) content = FindDeep(newGameObject.transform, contentName);
                    if (content == null)
                    {
                        var go = new GameObject("Content", typeof(UnityEngine.RectTransform));
                        go.transform.SetParent(viewport, false);
                        var rt = go.GetComponent<UnityEngine.RectTransform>();
                        if (direction == "vertical")
                        {
                            rt.anchorMin = new Vector2(0, 1);
                            rt.anchorMax = new Vector2(1, 1);
                            rt.pivot = new Vector2(0.5f, 1);
                        }
                        else
                        {
                            rt.anchorMin = new Vector2(0, 0);
                            rt.anchorMax = new Vector2(0, 1);
                            rt.pivot = new Vector2(0, 0.5f);
                        }

                        content = go.transform;
                    }

					// Layout + Fitter
					bool useGrid = AutoUIUtil.IsComponentExist(in layer, "grid");
					if (useGrid)
					{
						var grid = content.GetComponent<GridLayoutGroup>();
						if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
						// 自动推导 Grid 参数（沿用组层逻辑）
						GameObject contentGo = content.gameObject;
						AutoUILayoutProcessor.GridLayout参数自动推导(in layer, ref contentGo);

						var fitter = content.GetComponent<ContentSizeFitter>();
						if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
						if (direction == "vertical")
						{
							fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
							fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
						}
						else
						{
							fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
							fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
						}
					}
					else if (direction == "vertical")
					{
						var vg = content.GetComponent<VerticalLayoutGroup>();
						if (vg == null) vg = content.gameObject.AddComponent<VerticalLayoutGroup>();
						var fitter = content.GetComponent<ContentSizeFitter>();
						if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
						fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
						fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
					}
					else
					{
						var hg = content.GetComponent<HorizontalLayoutGroup>();
						if (hg == null) hg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
						var fitter = content.GetComponent<ContentSizeFitter>();
						if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
						fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
						fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
					}

                    // 绑定 ScrollRect
                    scroll.viewport = viewport as RectTransform;
                    scroll.content = content as RectTransform;
                    scroll.vertical = direction == "vertical";
                    scroll.horizontal = direction == "horizontal";
                    scroll.movementType = movement;
                    scroll.inertia = inertia;
                    scroll.decelerationRate = deceleration;
                    scroll.elasticity = elasticity;
                }
                catch (System.Exception ex)
                {
                    throw new System.Exception("Scroll装配失败: Layer='" + layer.name + "'. " + ex.Message, ex);
                }
            }
        }

        // 子物体创建完成后再收尾：自动绑定 Slider 的 fillRect / handleRect，并应用参数
        public static void AfterChildrenCreated(in Layer layer, ref GameObject go)
        {
            if (!(AutoUIUtil.IsComponentExist(in layer, "slider") ||
                  AutoUIUtil.IsComponentExist(in layer, "progress") || AutoUIUtil.IsComponentExist(in layer, "toggle")))
            {
                return;
            }

            // 共用的查找工具
            Transform FindDeep(Transform root, string name)
            {
                if (root == null || string.IsNullOrEmpty(name)) return null;
                if (root.name == name) return root;
                for (int i = 0; i < root.childCount; i++)
                {
                    var r = FindDeep(root.GetChild(i), name);
                    if (r != null) return r;
                }

                return null;
            }

            Transform GuessByKeywords(Transform root, params string[] keywords)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    var t = root.GetChild(i);
                    string lower = t.name.ToLower();
                    bool ok = false;
                    for (int k = 0; k < keywords.Length; k++)
                    {
                        if (lower.Contains(keywords[k]))
                        {
                            ok = true;
                            break;
                        }
                    }

                    if (ok && t.GetComponent<UnityEngine.UI.Image>() != null) return t;
                    var deep = GuessByKeywords(t, keywords);
                    if (deep != null) return deep;
                }

                return null;
            }

            // Toggle 处理
            Transform bg;
            string backgroundName;
            if (AutoUIUtil.IsComponentExist(in layer, "toggle"))
            {
                var toggle = go.GetComponent<UnityEngine.UI.Toggle>();
                if (toggle == null)
                {
                    toggle = go.AddComponent<UnityEngine.UI.Toggle>();
                }

                bool isOn = false;
                bool interactable = toggle.interactable;
                bool interactableSet = false;
                backgroundName = null;
                string checkmarkName = null;
                if (layer.components != null)
                {
                    foreach (var c in layer.components)
                    {
                        if (c.name == "toggle")
                        {
                            object o;
                            if (c.parameters != null)
                            {
                                if (c.parameters.TryGetValue("isOn", out o)) isOn = System.Convert.ToBoolean(o);
                                if (c.parameters.TryGetValue("interactable", out o))
                                {
                                    interactable = System.Convert.ToBoolean(o);
                                    interactableSet = true;
                                }

                                if (c.parameters.TryGetValue("background", out o)) backgroundName = o as string;
                                if (c.parameters.TryGetValue("checkmark", out o)) checkmarkName = o as string;
                            }

                            break;
                        }
                    }
                }

                // 绑定背景 -> targetGraphic
                bg = null;
                if (!string.IsNullOrEmpty(backgroundName)) bg = FindDeep(go.transform, backgroundName);
                if (bg == null) bg = GuessByKeywords(go.transform, "background", "bg", "box");
                if (bg != null)
                {
                    var img = bg.GetComponent<UnityEngine.UI.Image>();
                    if (img == null) img = bg.gameObject.AddComponent<UnityEngine.UI.Image>();
                    toggle.targetGraphic = img;
                }

                // 绑定选中图标 -> graphic
                Transform cm = null;
                if (!string.IsNullOrEmpty(checkmarkName)) cm = FindDeep(go.transform, checkmarkName);
                if (cm == null) cm = GuessByKeywords(go.transform, "check", "checkmark", "tick");
                if (cm == null)
                {
                    LogUtil.LogWarning("Toggle 未找到 Checkmark 节点: " + go.name + "，请在组件参数中指定 checkmark");
                }
                else
                {
                    var img = cm.GetComponent<UnityEngine.UI.Image>();
                    if (img == null) img = cm.gameObject.AddComponent<UnityEngine.UI.Image>();
                    toggle.graphic = img;
                }

                // 应用数值
                toggle.isOn = isOn;
                if (interactableSet) toggle.interactable = interactable;
            }

            // Slider/Progress 处理
            var slider = go.GetComponent<UnityEngine.UI.Slider>();
            string fillName;
            string handleName;
            if (AutoUIUtil.IsComponentExist(in layer, "slider") || AutoUIUtil.IsComponentExist(in layer, "progress"))
            {
                if (slider == null)
                {
                    // 安全兜底：若前置未挂，补挂
                    slider = go.AddComponent<UnityEngine.UI.Slider>();
                }

                // 从参数读取配置
                float min = 0f, max = 1f, value = 0f;
                bool wholeNumbers = false, interactableSet = false, interactable = slider.interactable;
                UnityEngine.UI.Slider.Direction direction = UnityEngine.UI.Slider.Direction.LeftToRight;
                // 注意：不要在此处重置名称变量，名称来自参数或关键词
                if (layer.components != null)
                {
                    foreach (var c in layer.components)
                    {
                        if (c.name == "slider" || c.name == "progress")
                        {
                            if (c.parameters != null)
                            {
                                object o;
                                if (c.parameters.TryGetValue("min", out o)) min = System.Convert.ToSingle(o);
                                if (c.parameters.TryGetValue("max", out o)) max = System.Convert.ToSingle(o);
                                if (c.parameters.TryGetValue("value", out o)) value = System.Convert.ToSingle(o);
                                if (c.parameters.TryGetValue("wholeNumbers", out o))
                                    wholeNumbers = System.Convert.ToBoolean(o);
                                if (c.parameters.TryGetValue("interactable", out o))
                                {
                                    interactable = System.Convert.ToBoolean(o);
                                    interactableSet = true;
                                }

                                if (c.parameters.TryGetValue("direction", out o))
                                {
                                    System.Enum.TryParse(o as string, true, out direction);
                                }

                                if (c.parameters.TryGetValue("background", out o)) backgroundName = o as string;
                                if (c.parameters.TryGetValue("fill", out o)) fillName = o as string;
                                if (c.parameters.TryGetValue("handle", out o)) handleName = o as string;
                            }

                            break;
                        }
                    }
                }

                // 应用基础数值
                slider.minValue = min;
                slider.maxValue = max;
                slider.wholeNumbers = wholeNumbers;
                slider.direction = direction;
                slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
                bool isProgress = AutoUIUtil.IsComponentExist(in layer, "progress");
                if (isProgress && !interactableSet)
                {
                    slider.interactable = false;
                }
                else
                {
                    slider.interactable = interactable;
                }
                
                fillName = null;
                // 绑定 fillRect（必需）
                Transform fill = null;
                if (!string.IsNullOrEmpty(fillName)) fill = FindDeep(go.transform, fillName);
                if (fill == null) fill = GuessByKeywords(go.transform, "fill", "bar", "progress");
                if (fill == null)
                {
                    LogUtil.LogWarning("Slider 未找到 Fill 节点: " + go.name + "，请在组件参数中指定 fill");
                }
                else
                {
                    // 确保 fill 上有 Image 便于视觉展示
                    var img = fill.GetComponent<UnityEngine.UI.Image>();
                    if (img == null) img = fill.gameObject.AddComponent<UnityEngine.UI.Image>();
                    slider.fillRect = fill as RectTransform;
                }

            }

            backgroundName = null;
            handleName = null;
            // 绑定 background -> targetGraphic（可选）
            bg = null;
            if (!string.IsNullOrEmpty(backgroundName)) bg = FindDeep(go.transform, backgroundName);
            if (bg == null) bg = GuessByKeywords(go.transform, "background", "bg");
            if (bg != null)
            {
                var img = bg.GetComponent<UnityEngine.UI.Image>();
                if (img == null) img = bg.gameObject.AddComponent<UnityEngine.UI.Image>();
                slider.targetGraphic = img;
            }


            // 绑定 handleRect（可选）
            Transform handle = null;
            if (!string.IsNullOrEmpty(handleName)) handle = FindDeep(go.transform, handleName);
            if (handle == null) handle = GuessByKeywords(go.transform, "handle", "thumb");
            if (handle != null)
            {
                var img = handle.GetComponent<UnityEngine.UI.Image>();
                if (img == null) img = handle.gameObject.AddComponent<UnityEngine.UI.Image>();
                slider.handleRect = handle as RectTransform;
            }
        }

        public static bool IsThisGroupAPrefab(in Layer layer)
        {
            if (layer.components != null)
            {
                foreach (var component in layer.components)
                {
                    if (component.name == "prefab")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string GetPrefabName(in Layer layer)
        {
            if (layer.components != null)
            {
                foreach (var component in layer.components)
                {
                    if (component.name == "prefab")
                    {
                        if (component.parameters != null)
                        {
                            component.parameters.TryGetValue("name", out object name);
                            string stringName = name as string;
                            if (stringName == null)
                            {
                                LogUtil.LogError("prefab组件的name参数不是string类型");
                            }

                            return stringName;
                        }

                        LogUtil.LogError("错误使用GetPrefabName,检查不到prefab组件的name参数");
                        return "";
                    }
                }
            }

            LogUtil.LogError("错误使用GetPrefabName,检查不到有解析的layer有prefab组件标记");
            return "";
        }

        public static bool HaveThisPrefabExist(string name)
        {
            foreach (var prefabName in ExistPrefabNames)
            {
                if (prefabName == name)
                {
                    return true;
                }
            }

            return false;
        }

        public static void AddPrefabToPrefabList(string name)
        {
            if (!HaveThisPrefabExist(name))
            {
                ExistPrefabNames.Add(name);
            }
        }
    }
}