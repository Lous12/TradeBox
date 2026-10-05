using System;
using System.Collections.Generic;
using System.Reflection;
using Lous12.TradeBox.Economy;
using Lous12.TradeBox.Data;
using Lous12.TradeBox.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace Lous12.TradeBox
{
    public partial class Main
    {
        private const string EconomyNativeTabName = "tradebox_city_economy_native_tab";
        private const string EconomyPanelName = "tradebox_city_economy_panel";
        private const float EconomyUiScanInterval = 0.75f;

        private CityWindow[] _cachedEconomyCityWindows = new CityWindow[0];
        private float _nextEconomyUiScanTime;
        private WindowMetaTab _economyNativeTab;
        private WindowMetaTabButtonsContainer _economyNativeContainer;
        private int _economyNativeWindowId = -1;
        private City _economyPanelCity;
        private int _economyPanelWindowId = -1;
        private Transform _economyDetachedPanel;

        private void ResetCityEconomyUiState()
        {
            _cachedEconomyCityWindows = new CityWindow[0];
            _nextEconomyUiScanTime = 0f;
            _economyNativeTab = null;
            _economyNativeContainer = null;
            _economyNativeWindowId = -1;
            _economyPanelCity = null;
            _economyPanelWindowId = -1;
            if (_economyDetachedPanel != null && _economyDetachedPanel.gameObject != null)
            {
                _economyDetachedPanel.gameObject.SetActive(false);
            }
            _economyDetachedPanel = null;
        }

        private void RefreshCityEconomyUi()
        {
            if (Time.unscaledTime < _nextEconomyUiScanTime)
            {
                return;
            }
            _nextEconomyUiScanTime = Time.unscaledTime + EconomyUiScanInterval;

            CityWindow active = FindActiveCityWindow();
            if (active == null)
            {
                if (_economyDetachedPanel != null && _economyDetachedPanel.gameObject != null)
                {
                    _economyDetachedPanel.gameObject.SetActive(false);
                }
                return;
            }

            EnsureNativeEconomyTab(active);

            Transform panel = FindEconomyPanel(active);
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                // Keep the panel object alive and only patch its text values.
                // This gives us live data without resetting the ScrollRect.
                PositionDetachedEconomyPanel(active, panel);
                RefreshEconomyPanelValues(active, panel);
                SyncEconomyTabContent(active);
            }
        }

        private CityWindow FindActiveCityWindow()
        {
            try
            {
                _cachedEconomyCityWindows = UnityEngine.Resources.FindObjectsOfTypeAll<CityWindow>();
            }
            catch
            {
                _cachedEconomyCityWindows = new CityWindow[0];
            }

            for (int i = 0; i < _cachedEconomyCityWindows.Length; i++)
            {
                CityWindow window = _cachedEconomyCityWindows[i];
                if (IsCityWindowFullyOpenForEconomy(window))
                {
                    return window;
                }
            }

            return null;
        }

        private static bool IsCityWindowFullyOpenForEconomy(CityWindow window)
        {
            if (
                window == null ||
                window.gameObject == null ||
                !window.gameObject.activeInHierarchy
            )
            {
                return false;
            }

            RectTransform rect = window.GetComponent<RectTransform>();
            if (rect == null)
            {
                return true;
            }

            Canvas canvas = window.GetComponentInParent<Canvas>();
            Camera camera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = canvas.worldCamera;
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 bl = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 tl = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 tr = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);

            float width = Mathf.Abs(tr.x - tl.x);
            float height = Mathf.Abs(tl.y - bl.y);

            return width >= 280f && height >= Screen.height * 0.55f;
        }

        private void EnsureNativeEconomyTab(CityWindow window)
        {
            if (window == null || window.gameObject == null)
            {
                return;
            }

            try
            {
                ScrollWindow scrollWindow = window.GetComponentInParent<ScrollWindow>();
                WindowMetaTabButtonsContainer container = scrollWindow != null ? scrollWindow.tabs : null;
                if (container == null)
                {
                    return;
                }

                container.init();
                int windowId = window.GetInstanceID();

                if (
                    _economyNativeTab != null &&
                    _economyNativeTab.gameObject != null &&
                    _economyNativeContainer == container &&
                    _economyNativeWindowId == windowId
                )
                {
                    _economyNativeTab.gameObject.SetActive(true);
                    return;
                }

                WindowMetaTab[] existingTabs = container.GetComponentsInChildren<WindowMetaTab>(true);
                for (int i = 0; i < existingTabs.Length; i++)
                {
                    WindowMetaTab existing = existingTabs[i];
                    if (existing != null && existing.gameObject != null && existing.gameObject.name == EconomyNativeTabName)
                    {
                        ConfigureNativeEconomyTab(window, container, existing);
                        return;
                    }
                }

                WindowMetaTab donor = container.tab_default;
                if (donor == null)
                {
                    for (int i = 0; i < existingTabs.Length; i++)
                    {
                        if (existingTabs[i] != null && existingTabs[i].gameObject != null && existingTabs[i].gameObject.activeSelf)
                        {
                            donor = existingTabs[i];
                            break;
                        }
                    }
                }

                if (donor == null || donor.gameObject == null || donor.transform.parent == null)
                {
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(donor.gameObject, donor.transform.parent, false);
                clone.SetActive(false);
                clone.name = EconomyNativeTabName;
                clone.transform.SetSiblingIndex(donor.transform.GetSiblingIndex());

                // A live vanilla tab donor may already contain runtime-only drag helpers
                // (and their Canvas/GraphicRaycaster) added by DragOrderElement.Start().
                // Cloning those helpers makes the cloned DragOrderElement try to add a
                // second Canvas and then crash. Economy is a fixed native tab, so strip
                // only those runtime drag-layer components before activating the clone.
                SanitizeClonedEconomyTab(clone);

                WindowMetaTab economyTab = clone.GetComponent<WindowMetaTab>();
                if (economyTab == null)
                {
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                ConfigureNativeEconomyTab(window, container, economyTab);
                LogInfo(
                    "[TradeBox-UI] Economy TRUE native WindowMetaTab created; donor=" +
                    donor.gameObject.name + ", parent=" + donor.transform.parent.name
                );
            }
            catch (Exception exception)
            {
                LogWarning("[TradeBox-UI] Economy tab unavailable: " + exception.Message);
            }
        }


        private static void SanitizeClonedEconomyTab(GameObject clone)
        {
            if (clone == null)
            {
                return;
            }

            Component[] components = clone.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (
                    typeName == "DragOrderElement" ||
                    component is Canvas ||
                    component is GraphicRaycaster ||
                    component is CanvasScaler
                )
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private void ConfigureNativeEconomyTab(
            CityWindow window,
            WindowMetaTabButtonsContainer container,
            WindowMetaTab tab
        )
        {
            int windowId = window.GetInstanceID();

            tab.tab_elements.Clear();
            tab.tab_action = new WindowMetaTabEvent();
            SetWindowMetaTabContainer(tab, container);
            RegisterWindowMetaTab(container, tab);
            ConfigureEconomyTabVisual(tab);

            tab.tab_action.AddListener(
                delegate(WindowMetaTab clickedTab)
                {
                    if (window == null || window.gameObject == null || !window.gameObject.activeInHierarchy)
                    {
                        return;
                    }

                    // Switch the native tab first. If another custom tab (for example
                    // Political World's Settlement Politics) owns the visible content,
                    // showTab() hides that content before TradeBox searches for the city
                    // host ScrollRect. Creating/showing Economy first could accidentally
                    // target the other tab's nested ScrollRect and then get hidden again.
                    container.showTab(clickedTab);
                    ShowCityEconomyPanel(window);
                    SyncEconomyTabContent(window);

                    // A first-ever Economy open can have an empty tab_elements list at
                    // showTab() time. ShowCityEconomyPanel creates the panel afterwards,
                    // so explicitly keep the newly registered content visible.
                    Transform economyPanel = FindEconomyPanel(window);
                    if (economyPanel != null)
                    {
                        economyPanel.gameObject.SetActive(true);
                    }
                }
            );

            tab.gameObject.SetActive(true);
            _economyNativeTab = tab;
            _economyNativeContainer = container;
            _economyNativeWindowId = windowId;
        }

        private static void SetWindowMetaTabContainer(
            WindowMetaTab tab,
            WindowMetaTabButtonsContainer container
        )
        {
            FieldInfo field = typeof(WindowMetaTab).GetField(
                "container",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (field != null)
            {
                field.SetValue(tab, container);
            }
        }

        private static void RegisterWindowMetaTab(
            WindowMetaTabButtonsContainer container,
            WindowMetaTab tab
        )
        {
            FieldInfo tabsField = typeof(WindowMetaTabButtonsContainer).GetField(
                "_tabs",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (tabsField == null)
            {
                throw new MissingFieldException("WindowMetaTabButtonsContainer._tabs");
            }

            List<WindowMetaTab> tabs = tabsField.GetValue(container) as List<WindowMetaTab>;
            if (tabs == null)
            {
                throw new InvalidOperationException("WindowMetaTabButtonsContainer._tabs is null");
            }

            if (!tabs.Contains(tab))
            {
                tabs.Add(tab);
            }
        }

        private static void ConfigureEconomyTabVisual(WindowMetaTab tab)
        {
            TipButton tip = tab.GetComponent<TipButton>();
            if (tip != null)
            {
                // TipButton treats these strings as localization keys. Keep them empty
                // until TradeBox ships localization entries instead of spamming missing-text logs.
                tip.textOnClick = "";
                tip.textOnClickDescription = "";
                tip.text_description_2 = "";
            }

            Image rootImage = tab.GetComponent<Image>();
            if (rootImage != null)
            {
                Sprite offSprite = SpriteTextureLoader.getSprite("ui/tab_button_vertical");
                if (offSprite != null)
                {
                    rootImage.sprite = offSprite;
                }
            }

            Image[] images = tab.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != rootImage)
                {
                    images[i].enabled = false;
                }
            }

            Transform oldLabel = tab.transform.Find("tradebox_economy_tab_letter");
            if (oldLabel != null)
            {
                UnityEngine.Object.Destroy(oldLabel.gameObject);
            }

            GameObject labelObject = new GameObject(
                "tradebox_economy_tab_letter",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            labelObject.transform.SetParent(tab.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "E";
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.94f, 0.84f, 0.42f, 1f);
            text.raycastTarget = false;
        }

        private void ShowCityEconomyPanel(CityWindow window)
        {
            if (window == null)
            {
                return;
            }

            City city = window.meta_object;
            if (city == null)
            {
                return;
            }

            CityEconomy economy = _cityEconomies.GetOrCreate(city);
            if (economy == null)
            {
                return;
            }
            if (economy.Resources.Count == 0)
            {
                _cityEconomies.Capture(city);
            }

            ScrollRect hostScroll = FindActiveCityStatsScroll(window);
            if (hostScroll == null)
            {
                LogWarning("[TradeBox-UI] City stats ScrollRect not found.");
                return;
            }

            Transform old = FindEconomyPanel(window);
            if (old != null)
            {
                _economyPanelCity = city;
                _economyPanelWindowId = window.GetInstanceID();
                _economyDetachedPanel = old;
                PositionDetachedEconomyPanel(window, old);
                old.gameObject.SetActive(true);
                RefreshEconomyPanelValues(window, old);
                SyncEconomyTabContent(window);
                return;
            }

            RectTransform overlayParent = GetEconomyOverlayParent(window);
            if (overlayParent == null)
            {
                LogWarning("[TradeBox-UI] ScrollWindow overlay parent not found.");
                return;
            }

            GameObject panelObject = new GameObject(
                EconomyPanelName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            // Important: keep Economy outside CityWindow's transform tree.
            // Settlement Politics scans CityWindow.GetComponentsInChildren<ScrollRect>();
            // a sibling overlay means its nested Economy ScrollRect is invisible to that scan.
            panelObject.transform.SetParent(overlayParent, false);
            panelObject.transform.SetAsLastSibling();
            LogInfo(
                "[TradeBox-UI] Detached Economy panel created; parent=" +
                overlayParent.gameObject.name + ", host=" + hostScroll.gameObject.name
            );

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);

            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.20f, 0.22f, 0.19f, 1f);
            background.raycastTarget = true;

            GameObject viewportObject = new GameObject(
                "tradebox_economy_viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect)
            );
            viewportObject.transform.SetParent(panelObject.transform, false);

            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = new GameObject(
                "tradebox_economy_content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            contentObject.transform.SetParent(viewportObject.transform, false);

            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup column = contentObject.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.spacing = 3f;
            column.padding = new RectOffset(2, 2, 4, 8);
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 22f;

            Text template = FindEconomyTextTemplate(window);
            RenderEconomyOverview(economy, contentObject.transform, template);

            _economyPanelCity = city;
            _economyPanelWindowId = window.GetInstanceID();
            _economyDetachedPanel = panelObject.transform;
            PositionDetachedEconomyPanel(window, panelObject.transform);
            panelObject.SetActive(true);
            RefreshEconomyPanelValues(window, panelObject.transform);
            scroll.verticalNormalizedPosition = 1f;
        }

        private static RectTransform GetEconomyOverlayParent(CityWindow window)
        {
            if (window == null)
            {
                return null;
            }

            ScrollWindow scrollWindow = window.GetComponentInParent<ScrollWindow>();
            Transform windowRoot = scrollWindow != null
                ? scrollWindow.transform
                : window.transform;

            // Use the parent of the whole CityWindow/ScrollWindow, not the window
            // itself. This guarantees Economy's ScrollRect is a sibling overlay and
            // therefore is not returned by CityWindow.GetComponentsInChildren().
            Transform parent = windowRoot != null ? windowRoot.parent : null;
            return parent != null ? parent.GetComponent<RectTransform>() : null;
        }

        private static void PositionDetachedEconomyPanel(CityWindow window, Transform panel)
        {
            if (window == null || panel == null)
            {
                return;
            }

            RectTransform panelRect = panel as RectTransform;
            RectTransform parentRect = panel.parent as RectTransform;
            ScrollRect hostScroll = FindActiveCityStatsScroll(window);
            RectTransform hostRect = hostScroll != null
                ? (hostScroll.viewport != null
                    ? hostScroll.viewport
                    : hostScroll.GetComponent<RectTransform>())
                : null;

            if (panelRect == null || parentRect == null || hostRect == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            hostRect.GetWorldCorners(corners);
            Vector3 localBottomLeft = parentRect.InverseTransformPoint(corners[0]);
            Vector3 localTopRight = parentRect.InverseTransformPoint(corners[2]);
            Vector3 center = (localBottomLeft + localTopRight) * 0.5f;

            panelRect.localPosition = new Vector3(center.x, center.y, 0f);
            panelRect.sizeDelta = new Vector2(
                Mathf.Abs(localTopRight.x - localBottomLeft.x),
                Mathf.Abs(localTopRight.y - localBottomLeft.y)
            );
            panelRect.localScale = Vector3.one;
        }

        private void RenderEconomyOverview(CityEconomy economy, Transform parent, Text template)
        {
            float coverage = CityProsperityCalculator.CalculateCoverageScore(economy);
            float trendScore = CityProsperityCalculator.CalculateTrendScore(economy);

            AddEconomyHeader(parent, template, "TRADEBOX // ECONOMY");
            AddEconomyRow(parent, template, "Prosperity", economy.Prosperity.ToString("0.0") + " / 100", GetProsperityColor(economy.Prosperity), "summary_prosperity");
            AddEconomyRow(parent, template, "Reserve coverage", coverage.ToString("0.0") + "%", GetCoverageColor(coverage), "summary_coverage");
            AddEconomyRow(parent, template, "Trend health", trendScore.ToString("0.0") + "%", GetCoverageColor(trendScore), "summary_trend");

            AddEconomySpacer(parent, 7f);
            AddEconomyHeader(parent, template, "RESOURCES");

            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                ResourceBalance balance = economy.GetOrCreateResource(resourceId);
                AddEconomyResourceRow(parent, template, resourceId, balance);
            }

            AddEconomySpacer(parent, 7f);
            AddEconomyHeader(parent, template, "INTERNAL TRADE");
            AddEconomyRow(parent, template, "Mode", "Same-state / nearest source", new Color(0.83f, 0.86f, 0.76f, 1f), "trade_mode");
            AddEconomyRow(parent, template, "Transfers", "0", new Color(0.90f, 0.82f, 0.45f, 1f), "trade_count");
            AddEconomyRow(parent, template, "Latest", "—", new Color(0.84f, 0.84f, 0.76f, 1f), "trade_recent_0", 34f);
            AddEconomyRow(parent, template, "", "—", new Color(0.84f, 0.84f, 0.76f, 1f), "trade_recent_1", 34f);
            AddEconomyRow(parent, template, "", "—", new Color(0.84f, 0.84f, 0.76f, 1f), "trade_recent_2", 34f);
        }

        private static void AddEconomyResourceRow(
            Transform parent,
            Text template,
            string resourceId,
            ResourceBalance balance
        )
        {
            Color color;
            string value = FormatResourceValue(balance, out color);
            AddEconomyRow(
                parent,
                template,
                GetResourceDisplayName(resourceId),
                value,
                color,
                "resource_" + resourceId,
                42f
            );
        }

        private static string GetResourceDisplayName(string resourceId)
        {
            if (resourceId == "wood") return "Wood";
            if (resourceId == "stone") return "Stone";
            if (resourceId == "common_metals") return "Metals";
            if (resourceId == "gold") return "Gold";
            return resourceId;
        }


        private static string FormatResourceValue(ResourceBalance balance, out Color color)
        {
            string trend = balance.Trend > 0 ? "+" + balance.Trend : balance.Trend.ToString();
            string status;
            if (balance.Deficit > 0)
            {
                status = "DEFICIT -" + balance.Deficit;
                color = new Color(0.94f, 0.48f, 0.40f, 1f);
            }
            else if (balance.Surplus > 0)
            {
                status = "SURPLUS +" + balance.Surplus;
                color = new Color(0.52f, 0.90f, 0.54f, 1f);
            }
            else
            {
                status = "BALANCED";
                color = new Color(0.84f, 0.84f, 0.76f, 1f);
            }

            return
                balance.Stock + " / " + balance.TargetReserve +
                "   Δ " + trend +
                "\n" + (balance.Coverage * 100f).ToString("0") + "%   " + status;
        }

        private static Color GetProsperityColor(float value)
        {
            if (value >= 75f) return new Color(0.52f, 0.90f, 0.54f, 1f);
            if (value >= 45f) return new Color(0.94f, 0.82f, 0.42f, 1f);
            return new Color(0.94f, 0.48f, 0.40f, 1f);
        }

        private static Color GetCoverageColor(float value)
        {
            if (value >= 80f) return new Color(0.52f, 0.90f, 0.54f, 1f);
            if (value >= 50f) return new Color(0.94f, 0.82f, 0.42f, 1f);
            return new Color(0.94f, 0.48f, 0.40f, 1f);
        }

        private static void AddEconomySpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject(
                "tradebox_economy_spacer",
                typeof(RectTransform),
                typeof(LayoutElement)
            );
            spacer.transform.SetParent(parent, false);
            LayoutElement layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static void AddEconomyHeader(Transform parent, Text template, string textValue)
        {
            GameObject rowObject = new GameObject(
                "tradebox_economy_header",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement)
            );
            rowObject.transform.SetParent(parent, false);

            LayoutElement layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = 25f;
            layout.preferredHeight = 25f;

            Image background = rowObject.GetComponent<Image>();
            background.color = new Color(0.13f, 0.15f, 0.12f, 0.90f);
            background.raycastTarget = false;

            Text title = CreateEconomyText(
                rowObject.transform,
                template,
                "title",
                textValue,
                new Color(0.94f, 0.82f, 0.45f, 1f),
                TextAnchor.MiddleLeft,
                11
            );
            RectTransform r = title.rectTransform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(8f, 1f);
            r.offsetMax = new Vector2(-8f, -1f);
            title.fontStyle = FontStyle.Bold;
        }

        private static void AddEconomyRow(
            Transform parent,
            Text template,
            string labelText,
            string valueText,
            Color valueColor,
            string rowKey = null,
            float rowHeight = 30f
        )
        {
            GameObject rowObject = new GameObject(
                string.IsNullOrEmpty(rowKey) ? "tradebox_economy_row" : "tradebox_economy_row_" + rowKey,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement)
            );
            rowObject.transform.SetParent(parent, false);

            LayoutElement layout = rowObject.GetComponent<LayoutElement>();
            layout.minHeight = rowHeight;
            layout.preferredHeight = rowHeight;

            Image background = rowObject.GetComponent<Image>();
            bool even = (parent.childCount % 2) == 0;
            background.color = even
                ? new Color(0.22f, 0.24f, 0.20f, 0.76f)
                : new Color(0.20f, 0.22f, 0.19f, 0.76f);
            background.raycastTarget = false;

            Text label = CreateEconomyText(
                rowObject.transform,
                template,
                "label",
                labelText,
                new Color(0.80f, 0.81f, 0.77f, 1f),
                TextAnchor.MiddleLeft,
                10
            );
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0.36f, 1f);
            labelRect.offsetMin = new Vector2(8f, 1f);
            labelRect.offsetMax = new Vector2(-3f, -1f);

            Text value = CreateEconomyText(
                rowObject.transform,
                template,
                "value",
                valueText,
                valueColor,
                TextAnchor.MiddleRight,
                10
            );
            RectTransform valueRect = value.rectTransform;
            valueRect.anchorMin = new Vector2(0.36f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = new Vector2(3f, 1f);
            valueRect.offsetMax = new Vector2(-8f, -1f);
        }

        private static Text CreateEconomyText(
            Transform parent,
            Text template,
            string name,
            string textValue,
            Color color,
            TextAnchor alignment,
            int fontSize
        )
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            if (template != null)
            {
                text.font = template.font;
                text.fontStyle = template.fontStyle;
                text.material = template.material;
            }
            if (text.font == null)
            {
                text.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.text = textValue ?? "";
            text.color = color;
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Text FindEconomyTextTemplate(Component window)
        {
            if (window == null)
            {
                return null;
            }

            Text[] texts = window.GetComponentsInChildren<Text>(true);
            Text best = null;
            float bestScore = -1f;
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text.font == null)
                {
                    continue;
                }

                float score = 0f;
                if (text.fontSize >= 10 && text.fontSize <= 24) score += 10f;
                if (text.gameObject.activeInHierarchy) score += 5f;
                score += Mathf.Min(5f, text.fontSize * 0.1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = text;
                }
            }
            return best;
        }

        private static ScrollRect FindActiveCityStatsScroll(CityWindow window)
        {
            if (window == null)
            {
                return null;
            }

            ScrollRect[] scrolls = window.GetComponentsInChildren<ScrollRect>(true);
            ScrollRect best = null;
            float bestArea = 0f;
            for (int i = 0; i < scrolls.Length; i++)
            {
                ScrollRect scroll = scrolls[i];
                if (scroll == null || scroll.content == null || !scroll.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Ignore nested scroll views owned by custom CityWindow pages.
                // Otherwise opening Economy from Settlement Politics can make us
                // attach the Economy panel inside the Politics panel itself.
                if (IsNestedCustomCityTabScroll(scroll))
                {
                    continue;
                }

                RectTransform rect = scroll.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                float area = Mathf.Abs(rect.rect.width * rect.rect.height);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = scroll;
                }
            }
            return best;
        }


        private static bool IsNestedCustomCityTabScroll(ScrollRect scroll)
        {
            if (scroll == null)
            {
                return false;
            }

            Transform current = scroll.transform;
            while (current != null)
            {
                string objectName = current.gameObject != null
                    ? current.gameObject.name
                    : "";

                if (string.Equals(objectName, EconomyPanelName, StringComparison.Ordinal))
                {
                    return true;
                }

                // Political World names the embedded settlement-politics hierarchy
                // ukiol_city_politics_*. Keep this string-only compatibility check so
                // TradeBox remains fully standalone and never references PW assemblies.
                if (
                    !string.IsNullOrEmpty(objectName) &&
                    objectName.IndexOf("ukiol_city_politics", StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private Transform FindEconomyPanel(CityWindow window)
        {
            if (window == null)
            {
                return null;
            }

            int windowId = window.GetInstanceID();
            if (
                _economyDetachedPanel != null &&
                _economyDetachedPanel.gameObject != null &&
                _economyPanelWindowId == windowId
            )
            {
                return _economyDetachedPanel;
            }

            RectTransform overlayParent = GetEconomyOverlayParent(window);
            if (overlayParent == null)
            {
                return null;
            }

            RectTransform[] rects = overlayParent.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || rect.gameObject.name != EconomyPanelName)
                {
                    continue;
                }

                // Detached Economy panels are direct children of the CityWindow's
                // parent overlay. Do not pick stale copies nested inside CityWindow
                // from an older hot-reload session.
                if (rect.parent == overlayParent)
                {
                    _economyDetachedPanel = rect;
                    _economyPanelWindowId = windowId;
                    return rect;
                }
            }

            return null;
        }

        private void RefreshEconomyPanelValues(CityWindow window, Transform panel)
        {
            if (window == null || panel == null)
            {
                return;
            }

            City city = window.meta_object;
            if (city == null)
            {
                return;
            }

            CityEconomy economy = _cityEconomies.GetOrCreate(city);
            if (economy == null)
            {
                return;
            }
            if (economy.Resources.Count == 0)
            {
                _cityEconomies.Capture(city);
            }

            _economyPanelCity = city;
            _economyPanelWindowId = window.GetInstanceID();

            float coverage = CityProsperityCalculator.CalculateCoverageScore(economy);
            float trendScore = CityProsperityCalculator.CalculateTrendScore(economy);

            UpdateEconomyRowValue(
                panel,
                "summary_prosperity",
                economy.Prosperity.ToString("0.0") + " / 100",
                GetProsperityColor(economy.Prosperity)
            );
            UpdateEconomyRowValue(
                panel,
                "summary_coverage",
                coverage.ToString("0.0") + "%",
                GetCoverageColor(coverage)
            );
            UpdateEconomyRowValue(
                panel,
                "summary_trend",
                trendScore.ToString("0.0") + "%",
                GetCoverageColor(trendScore)
            );

            for (int i = 0; i < VanillaResourceIds.Core.Length; i++)
            {
                string resourceId = VanillaResourceIds.Core[i];
                ResourceBalance balance = economy.GetOrCreateResource(resourceId);
                Color resourceColor;
                string resourceValue = FormatResourceValue(balance, out resourceColor);
                UpdateEconomyRowValue(
                    panel,
                    "resource_" + resourceId,
                    resourceValue,
                    resourceColor
                );
            }

            int relatedTransfers = 0;
            int shownTransfers = 0;
            for (int i = TradeBoxState.RecentTransactions.Count - 1; i >= 0; i--)
            {
                var transaction = TradeBoxState.RecentTransactions[i];
                if (transaction == null)
                {
                    continue;
                }

                bool outgoing = string.Equals(
                    transaction.SourceCityId,
                    economy.CityId,
                    StringComparison.Ordinal
                );
                bool incoming = string.Equals(
                    transaction.TargetCityId,
                    economy.CityId,
                    StringComparison.Ordinal
                );
                if (!outgoing && !incoming)
                {
                    continue;
                }

                relatedTransfers++;
                if (shownTransfers >= 3)
                {
                    continue;
                }

                string resourceName = GetResourceDisplayName(transaction.ResourceId);
                string otherCity = outgoing
                    ? transaction.TargetCityName
                    : transaction.SourceCityName;
                if (string.IsNullOrEmpty(otherCity))
                {
                    otherCity = "City";
                }

                string transferText = outgoing
                    ? resourceName + " -" + transaction.Amount + "  → " + otherCity
                    : resourceName + " +" + transaction.Amount + "  ← " + otherCity;
                Color transferColor = outgoing
                    ? new Color(0.94f, 0.82f, 0.42f, 1f)
                    : new Color(0.52f, 0.90f, 0.54f, 1f);

                UpdateEconomyRowValue(
                    panel,
                    "trade_recent_" + shownTransfers,
                    transferText,
                    transferColor
                );
                shownTransfers++;
            }

            UpdateEconomyRowValue(
                panel,
                "trade_count",
                relatedTransfers.ToString(),
                new Color(0.90f, 0.82f, 0.45f, 1f)
            );

            while (shownTransfers < 3)
            {
                UpdateEconomyRowValue(
                    panel,
                    "trade_recent_" + shownTransfers,
                    "—",
                    new Color(0.84f, 0.84f, 0.76f, 1f)
                );
                shownTransfers++;
            }
        }

        private static void UpdateEconomyRowValue(
            Transform panel,
            string rowKey,
            string value,
            Color color
        )
        {
            if (panel == null || string.IsNullOrEmpty(rowKey))
            {
                return;
            }

            Transform valueTransform = panel.Find(
                "tradebox_economy_viewport/tradebox_economy_content/" +
                "tradebox_economy_row_" + rowKey + "/value"
            );
            if (valueTransform == null)
            {
                return;
            }

            Text text = valueTransform.GetComponent<Text>();
            if (text == null)
            {
                return;
            }

            text.text = value ?? "";
            text.color = color;
        }

        private void SyncEconomyTabContent(CityWindow window)
        {
            if (
                window == null ||
                _economyNativeTab == null ||
                _economyNativeWindowId != window.GetInstanceID()
            )
            {
                return;
            }

            Transform panel = FindEconomyPanel(window);
            _economyNativeTab.tab_elements.Clear();
            if (panel != null)
            {
                _economyNativeTab.tab_elements.Add(panel);
            }
        }
    }
}
