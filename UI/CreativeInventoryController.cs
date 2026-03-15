using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using MineMogul.CreativeMode.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using System;

namespace MineMogul.CreativeMode.UI;

internal sealed partial class CreativeInventoryController : System.IDisposable
{
    private static readonly string[] NativeInventorySuppressNameTokens = { "preview", "description", "selectediteminfo", "selecteditem", "iteminfo", "title" };
    private static readonly Color PanelColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
    private static readonly Color StripColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
    private static readonly Color TabNormalColor = new Color(0.28f, 0.28f, 0.28f, 1f);
    private static readonly Color TabSelectedColor = new Color(0.80f, 0.62f, 0.20f, 1f);
    private static readonly Color TabArrowColor = new Color(0.36f, 0.36f, 0.36f, 1f);

    private readonly ManualLogSource _logger;

    private readonly List<ShopCategory> _categories = new List<ShopCategory>();
    private readonly List<Button> _tabButtons = new List<Button>();
    private readonly Dictionary<ShopCategory, Button> _categoryTabButtons = new Dictionary<ShopCategory, Button>();
    private readonly Dictionary<ShopCategory, RectTransform> _categoryItemPages = new Dictionary<ShopCategory, RectTransform>();

    private GameObject _overlayCanvasRoot;
    private RectTransform _overlayCanvasRect;
    private GameObject _root;
    private RectTransform _frameRect;
    private Image _rootBackgroundImage;
    private RectTransform _tabButtonsRoot;
    private RectTransform _itemsViewportRect;
    private RectTransform _itemsGridRoot;
    private GridLayoutGroup _itemsGridLayout;
    private ScrollRect _itemsScrollRect;
    private Button _nextTabPageButton;
    private TMP_Text _tabPageText;
    private CreativeHoverTooltip _hoverTooltip;

    private ShopCategory _selectedCategory;
    private int _currentTabPage;
    private int _tabsPerPage = 8;
    private float _nextCategoryRefreshTime;

    private bool _creativeModeEnabled;
    private bool _wasUiVisible;
    private bool _nativePreviewWasActive = true;
    private readonly Dictionary<GameObject, bool> _suppressedNativeInventoryObjects = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Graphic, bool> _suppressedNativeInventoryGraphics = new Dictionary<Graphic, bool>();
    private GameObject _nativeInventoryBackgroundReplacement;
    private Image _nativeInventoryBackgroundReplacementImage;
    private BaseHeldTool _pendingCursorTool;
    private bool _usingCreativeDragGhost;
    private Transform _dragGhostOriginalParent;
    private int _dragGhostOriginalSiblingIndex = -1;

    internal CreativeInventoryController(ManualLogSource logger)
    {
        _logger = logger;
    }

    internal void SetCreativeModeEnabled(bool enabled)
    {
        _creativeModeEnabled = enabled;

        if (!enabled)
        {
            HideUi();
            SetNativePreviewActive(true);
            return;
        }

        bool inventoryVisible = IsInventoryVisible();
        if (inventoryVisible)
        {
            ShowUi(forceCategoryRefresh: true, inventoryVisible: inventoryVisible);
        }
    }

    internal void Tick(int tabsPerPage, bool isGameplayContext, bool inventoryVisible)
    {
        _tabsPerPage = Mathf.Max(1, tabsPerPage);

        if (!isGameplayContext)
        {
            if (_wasUiVisible || _root != null)
            {
                _wasUiVisible = false;
                HideUi();
            }

            return;
        }

        bool shouldShowUi = _creativeModeEnabled && inventoryVisible;
        if (shouldShowUi != _wasUiVisible)
        {
            _wasUiVisible = shouldShowUi;
            if (shouldShowUi)
            {
                ShowUi(forceCategoryRefresh: true, inventoryVisible: inventoryVisible);
            }
            else
            {
                HideUi();
            }
        }

        if (!shouldShowUi)
        {
            return;
        }

        EnsureUi();
        if (_root == null)
        {
            return;
        }

        if (_pendingCursorTool != null && !inventoryVisible)
        {
            CancelPendingCursorItem();
        }
        else if (_pendingCursorTool != null && inventoryVisible && Input.GetMouseButtonDown(1))
        {
            CancelPendingCursorItem();
        }

        if (_pendingCursorTool != null && inventoryVisible)
        {
            TryAddPendingCursorItemFromScroll();
        }

        if (_pendingCursorTool != null && inventoryVisible && Input.GetMouseButtonDown(0) && !IsPointerOverCreativePickupTarget())
        {
            DropPendingCursorItemToWorld();
        }

        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _hoverTooltip?.EnsureOnTop();
        bool inventoryMode = _creativeModeEnabled && inventoryVisible;

        if (inventoryMode)
        {
            ApplyNativeInventorySuppression();
        }
        else
        {
            RestoreSuppressedNativeInventoryObjects();
            SetNativePreviewActive(true);
        }

        ApplyLayoutMode(inventoryMode);
        UpdatePendingCursorGhost();
        _hoverTooltip?.EnsureOnTop();

        if (Time.unscaledTime >= _nextCategoryRefreshTime)
        {
            RefreshCategories(force: false);
            _nextCategoryRefreshTime = Time.unscaledTime + 1f;
        }
    }

    public void Dispose()
    {
        TearDownUi();
    }

    private void ShowUi(bool forceCategoryRefresh, bool inventoryVisible)
    {
        EnsureUi();
        if (_root == null)
        {
            return;
        }

        bool inventoryMode = _creativeModeEnabled && inventoryVisible;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();

        if (inventoryMode)
        {
            ApplyNativeInventorySuppression();
        }
        else
        {
            RestoreSuppressedNativeInventoryObjects();
            SetNativePreviewActive(true);
        }

        ApplyLayoutMode(inventoryMode);

        RefreshCategories(forceCategoryRefresh);
    }

    private void HideUi()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }

        _hoverTooltip?.Hide();
        CancelPendingCursorItem();
        RestoreSuppressedNativeInventoryObjects();
        SetNativePreviewActive(true);
    }

    private void EnsureUi()
    {
        if (_root != null)
        {
            return;
        }

        Transform parent = EnsureOverlayCanvasRoot();
        if (parent == null)
        {
            return;
        }

        BuildUi(parent);
    }

    private Transform EnsureOverlayCanvasRoot()
    {
        if (_overlayCanvasRoot == null)
        {
            EnsureEventSystem();

            _overlayCanvasRoot = new GameObject(
                "CreativeModeOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_overlayCanvasRoot);

            Canvas canvas = _overlayCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5500;

            _overlayCanvasRect = _overlayCanvasRoot.GetComponent<RectTransform>();

            CanvasScaler scaler = _overlayCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        return _overlayCanvasRoot.transform;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("CreativeModeEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Object.DontDestroyOnLoad(eventSystem);
    }

    private void BuildUi(Transform parent)
    {
        _root = CreateUiObject("CreativeModePanel", parent, typeof(Image), typeof(LayoutElement));
        _root.transform.SetAsLastSibling();
        RectTransform rootRect = (RectTransform)_root.transform;
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        _rootBackgroundImage = _root.GetComponent<Image>();
        _rootBackgroundImage.color = new Color(0f, 0f, 0f, 0f);
        _rootBackgroundImage.raycastTarget = false;

        LayoutElement layoutElement = _root.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        GameObject frame = CreateUiObject("CreativeModeFrame", rootRect, typeof(Image));
        _frameRect = (RectTransform)frame.transform;
        _frameRect.anchorMin = new Vector2(0.016f, 0.34f);
        _frameRect.anchorMax = new Vector2(0.984f, 0.985f);
        _frameRect.offsetMin = Vector2.zero;
        _frameRect.offsetMax = Vector2.zero;

        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = PanelColor;
        frameImage.raycastTarget = true;

        BuildTabBar(_frameRect);
        BuildItemsArea(_frameRect);
        _hoverTooltip = new CreativeHoverTooltip(_overlayCanvasRect, parent);
        ApplyLayoutMode(inventoryMode: false);
    }

    private void ApplyLayoutMode(bool inventoryMode)
    {
        if (_frameRect == null)
        {
            return;
        }

        if (inventoryMode)
        {
            if (!TryAlignFrameToInventory())
            {
                _frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                _frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                _frameRect.pivot = new Vector2(0.5f, 0.5f);
                _frameRect.anchoredPosition = new Vector2(0f, 295f);
                _frameRect.sizeDelta = new Vector2(860f, 410f);
            }

            if (_rootBackgroundImage != null)
            {
                _rootBackgroundImage.color = new Color(0f, 0f, 0f, 0f);
                _rootBackgroundImage.raycastTarget = false;
            }

            UpdateItemsGridLayout();
            return;
        }

        _frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        _frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        _frameRect.pivot = new Vector2(0.5f, 0.5f);
        _frameRect.anchoredPosition = Vector2.zero;
        _frameRect.sizeDelta = new Vector2(1520f, 860f);

        if (_rootBackgroundImage != null)
        {
            _rootBackgroundImage.color = new Color(0f, 0f, 0f, 0.56f);
            _rootBackgroundImage.raycastTarget = true;
        }

        UpdateItemsGridLayout();
    }

    private bool TryAlignFrameToInventory()
    {
        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        if (_overlayCanvasRect == null || inventoryUi == null || inventoryUi.InventoryPanel == null)
        {
            return false;
        }

        RectTransform inventoryRect = inventoryUi.InventoryPanel.GetComponent<RectTransform>();
        if (inventoryRect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        inventoryRect.GetWorldCorners(corners);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayCanvasRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[0]),
            null,
            out Vector2 bottomLeft))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlayCanvasRect,
            RectTransformUtility.WorldToScreenPoint(null, corners[2]),
            null,
            out Vector2 topRight))
        {
            return false;
        }

        float inventoryWidth = Mathf.Abs(topRight.x - bottomLeft.x);
        float inventoryTop = topRight.y;
        float frameWidth = Mathf.Clamp(inventoryWidth, 540f, _overlayCanvasRect.rect.width - 48f);
        float gapAboveInventory = 8f;
        float inventoryOverlapAllowance = 320f;
        float rowHeight = _itemsGridLayout != null ? _itemsGridLayout.cellSize.y : 72f;
        float rowSpacing = _itemsGridLayout != null ? _itemsGridLayout.spacing.y : 6f;
        float desiredViewportHeight = (rowHeight * 4f) + (rowSpacing * 3f) + 140f;
        float minimumFrameHeight = desiredViewportHeight + 180f;
        float frameHeight = Mathf.Clamp(minimumFrameHeight, 520f, 760f);
        float centerX = (bottomLeft.x + topRight.x) * 0.5f;
        float bottomY = inventoryTop + gapAboveInventory - inventoryOverlapAllowance;
        float topLimitY = _overlayCanvasRect.rect.yMax - 12f;
        float topY = bottomY + frameHeight;
        if (topY > topLimitY)
        {
            bottomY -= topY - topLimitY;
        }

        float centerY = bottomY + (frameHeight * 0.5f);

        _frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        _frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        _frameRect.pivot = new Vector2(0.5f, 0.5f);
        _frameRect.anchoredPosition = new Vector2(centerX, centerY);
        _frameRect.sizeDelta = new Vector2(frameWidth, frameHeight);
        return true;
    }

    private void UpdateItemsGridLayout()
    {
        if (_itemsViewportRect == null || _itemsGridLayout == null)
        {
            return;
        }

        float usableWidth = Mathf.Max(120f, _itemsViewportRect.rect.width - 12f);
        float cellWidth = _itemsGridLayout.cellSize.x;
        float spacing = _itemsGridLayout.spacing.x;
        int columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + spacing) / (cellWidth + spacing)));
        _itemsGridLayout.constraintCount = columns;
    }

    private void RebuildCachedCategoryUi()
    {
        ClearCategoryTabs();
        ClearItemButtons();

        if (_tabButtonsRoot == null || _itemsViewportRect == null)
        {
            return;
        }

        for (int i = 0; i < _categories.Count; i++)
        {
            ShopCategory category = _categories[i];
            Button tabButton = CreateCategoryTabButton(_tabButtonsRoot, category);
            _tabButtons.Add(tabButton);
            _categoryTabButtons[category] = tabButton;

            RectTransform itemPage = CreateCategoryItemPage(_itemsViewportRect, category);
            _categoryItemPages[category] = itemPage;
        }

        RebuildCategoryTabs();
        ShowSelectedCategoryPage();
    }

    private void BuildTabBar(RectTransform rootRect)
    {
        GameObject strip = CreateUiObject("TabStrip", rootRect, typeof(Image));
        RectTransform stripRect = (RectTransform)strip.transform;
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta = new Vector2(0f, 58f);

        Image stripImage = strip.GetComponent<Image>();
        stripImage.color = StripColor;

        GameObject tabsRoot = CreateUiObject("Tabs", stripRect, typeof(HorizontalLayoutGroup));
        _tabButtonsRoot = (RectTransform)tabsRoot.transform;
        _tabButtonsRoot.anchorMin = new Vector2(0f, 0f);
        _tabButtonsRoot.anchorMax = new Vector2(1f, 1f);
        _tabButtonsRoot.offsetMin = new Vector2(6f, 0f);
        _tabButtonsRoot.offsetMax = new Vector2(-110f, 0f);

        HorizontalLayoutGroup tabsRowLayout = tabsRoot.GetComponent<HorizontalLayoutGroup>();
        tabsRowLayout.spacing = 24f;
        tabsRowLayout.childControlWidth = false;
        tabsRowLayout.childControlHeight = false;
        tabsRowLayout.childForceExpandWidth = false;
        tabsRowLayout.childForceExpandHeight = false;
        tabsRowLayout.childAlignment = TextAnchor.UpperCenter;

        GameObject pageButton = CreateUiObject("NextPageButton", stripRect, typeof(Image), typeof(Button));
        _nextTabPageButton = pageButton.GetComponent<Button>();
        RectTransform pageButtonRect = (RectTransform)pageButton.transform;
        pageButtonRect.anchorMin = new Vector2(1f, 0.5f);
        pageButtonRect.anchorMax = new Vector2(1f, 0.5f);
        pageButtonRect.pivot = new Vector2(1f, 0.5f);
        pageButtonRect.anchoredPosition = new Vector2(-6f, 0f);
        pageButtonRect.sizeDelta = new Vector2(46f, 46f);

        Image pageButtonImage = pageButton.GetComponent<Image>();
        pageButtonImage.color = TabArrowColor;

        ColorBlock arrowColors = _nextTabPageButton.colors;
        arrowColors.normalColor = TabArrowColor;
        arrowColors.highlightedColor = new Color(0.46f, 0.46f, 0.46f, 1f);
        arrowColors.pressedColor = new Color(0.60f, 0.60f, 0.60f, 1f);
        arrowColors.selectedColor = arrowColors.highlightedColor;
        arrowColors.colorMultiplier = 1f;
        _nextTabPageButton.colors = arrowColors;
        _nextTabPageButton.onClick.AddListener(OnNextTabPagePressed);
        CreativeHoverTooltipTarget nextPageTooltip = pageButton.AddComponent<CreativeHoverTooltipTarget>();
        nextPageTooltip.Initialize(_hoverTooltip, "Next tab page");

        GameObject arrowLabel = CreateUiObject("ArrowLabel", pageButton.transform, typeof(TextMeshProUGUI));
        RectTransform arrowRect = (RectTransform)arrowLabel.transform;
        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;

        TextMeshProUGUI arrowText = arrowLabel.GetComponent<TextMeshProUGUI>();
        arrowText.text = ">";
        arrowText.alignment = TextAlignmentOptions.Center;
        arrowText.fontSize = 24f;
        arrowText.color = Color.white;
        arrowText.raycastTarget = false;

        GameObject pageTextObject = CreateUiObject("PageText", stripRect, typeof(TextMeshProUGUI));
        RectTransform pageTextRect = (RectTransform)pageTextObject.transform;
        pageTextRect.anchorMin = new Vector2(1f, 0.5f);
        pageTextRect.anchorMax = new Vector2(1f, 0.5f);
        pageTextRect.pivot = new Vector2(1f, 0.5f);
        pageTextRect.anchoredPosition = new Vector2(-56f, 0f);
        pageTextRect.sizeDelta = new Vector2(44f, 46f);
        _tabPageText = pageTextObject.GetComponent<TextMeshProUGUI>();
        _tabPageText.text = "1/1";
        _tabPageText.alignment = TextAlignmentOptions.Center;
        _tabPageText.fontSize = 16f;
        _tabPageText.color = Color.white;
        _tabPageText.raycastTarget = false;
    }

    private void BuildItemsArea(RectTransform rootRect)
    {
        GameObject viewportObject = CreateUiObject("ItemsViewport", rootRect, typeof(Image), typeof(Mask), typeof(ScrollRect));
        _itemsViewportRect = (RectTransform)viewportObject.transform;
        _itemsViewportRect.anchorMin = new Vector2(0f, 0f);
        _itemsViewportRect.anchorMax = new Vector2(1f, 1f);
        _itemsViewportRect.offsetMin = new Vector2(8f, 12f);
        _itemsViewportRect.offsetMax = new Vector2(-8f, -74f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.28f);

        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        _itemsScrollRect = viewportObject.GetComponent<ScrollRect>();
        _itemsScrollRect.viewport = _itemsViewportRect;
        _itemsScrollRect.content = null;
        _itemsScrollRect.horizontal = false;
        _itemsScrollRect.vertical = true;
        _itemsScrollRect.scrollSensitivity = 35f;
        _itemsScrollRect.movementType = ScrollRect.MovementType.Clamped;

        _itemsGridRoot = null;
        _itemsGridLayout = null;
    }

    private void RefreshCategories(bool force)
    {
        List<ShopCategory> latestCategories = GetLatestCategories();

        if (!force && !DidCategoriesChange(latestCategories))
        {
            return;
        }

        _categories.Clear();
        _categories.AddRange(latestCategories);

        NormalizePageAndSelection();
        RebuildCachedCategoryUi();
    }

    private List<ShopCategory> GetLatestCategories()
    {
        EconomyManager economyManager = Singleton<EconomyManager>.Instance;
        if (economyManager == null)
        {
            return new List<ShopCategory>();
        }

        return economyManager
            .GetAvailableShopCategories()
            .Where(IsUsableCategory)
            .ToList();
    }

    private bool DidCategoriesChange(List<ShopCategory> latestCategories)
    {
        if (_categories.Count != latestCategories.Count)
        {
            return true;
        }

        for (int i = 0; i < _categories.Count; i++)
        {
            ShopCategory current = _categories[i];
            ShopCategory latest = latestCategories[i];
            if (current != latest)
            {
                return true;
            }

            int currentCount = CountUsableItems(current);
            int latestCount = CountUsableItems(latest);
            if (currentCount != latestCount)
            {
                return true;
            }
        }

        return false;
    }

    private void NormalizePageAndSelection()
    {
        if (_categories.Count == 0)
        {
            _selectedCategory = null;
            _currentTabPage = 0;
            return;
        }

        if (_selectedCategory == null || !_categories.Contains(_selectedCategory))
        {
            _selectedCategory = _categories[0];
        }

        int selectedIndex = _categories.IndexOf(_selectedCategory);
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            _selectedCategory = _categories[0];
        }

        int totalPages = GetTotalTabPages();
        _currentTabPage = Mathf.Clamp(selectedIndex / _tabsPerPage, 0, totalPages - 1);
    }

    private void RebuildCategoryTabs()
    {
        if (_tabButtonsRoot == null)
        {
            return;
        }

        if (_categories.Count == 0)
        {
            if (_nextTabPageButton != null)
            {
                _nextTabPageButton.gameObject.SetActive(false);
            }

            if (_tabPageText != null)
            {
                _tabPageText.text = "0/0";
            }

            return;
        }

        int startIndex = _currentTabPage * _tabsPerPage;
        int endIndex = Mathf.Min(startIndex + _tabsPerPage, _categories.Count);
        for (int i = 0; i < _categories.Count; i++)
        {
            ShopCategory category = _categories[i];
            if (!_categoryTabButtons.TryGetValue(category, out Button tabButton) || tabButton == null)
            {
                continue;
            }

            bool isVisibleOnPage = i >= startIndex && i < endIndex;
            tabButton.gameObject.SetActive(isVisibleOnPage);
        }

        UpdateVisibleTabSizes(startIndex, endIndex);
        RefreshTabVisuals();

        int totalPages = GetTotalTabPages();
        if (_nextTabPageButton != null)
        {
            _nextTabPageButton.gameObject.SetActive(totalPages > 1);
        }

        if (_tabPageText != null)
        {
            _tabPageText.text = (totalPages <= 0) ? "0/0" : $"{_currentTabPage + 1}/{totalPages}";
        }
    }

    private void UpdateVisibleTabSizes(int startIndex, int endIndex)
    {
        if (_tabButtonsRoot == null)
        {
            return;
        }

        int visibleCount = Mathf.Max(0, endIndex - startIndex);
        if (visibleCount <= 0)
        {
            return;
        }

        HorizontalLayoutGroup rowLayout = _tabButtonsRoot.GetComponent<HorizontalLayoutGroup>();
        float spacing = rowLayout != null ? rowLayout.spacing : 0f;
        float availableWidth = Mathf.Max(0f, _tabButtonsRoot.rect.width - (spacing * Mathf.Max(0, visibleCount - 1)));
        float targetWidth = Mathf.Max(32f, availableWidth / visibleCount);
        float targetHeight = Mathf.Max(32f, _tabButtonsRoot.rect.height);

        for (int i = 0; i < _categories.Count; i++)
        {
            ShopCategory category = _categories[i];
            if (!_categoryTabButtons.TryGetValue(category, out Button tabButton) || tabButton == null)
            {
                continue;
            }

            LayoutElement layout = tabButton.GetComponent<LayoutElement>();
            RectTransform rect = tabButton.transform as RectTransform;
            if (layout == null || rect == null)
            {
                continue;
            }

            bool isVisibleOnPage = i >= startIndex && i < endIndex;
            float width = isVisibleOnPage ? targetWidth : 54f;
            float height = isVisibleOnPage ? targetHeight : 54f;

            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.flexibleWidth = 0f;
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    private Button CreateCategoryTabButton(Transform parent, ShopCategory category)
    {
        GameObject buttonObject = CreateUiObject(
            "CategoryTab_" + CreativeNameUtils.SanitizeName(category.CategoryName, "Category"),
            parent,
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));

        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.sizeDelta = new Vector2(54f, 54f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 54f;
        layout.preferredHeight = 54f;
        layout.minWidth = 54f;
        layout.minHeight = 54f;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = TabNormalColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = TabNormalColor;
        colors.highlightedColor = new Color(0.44f, 0.44f, 0.44f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Sprite tabIcon = GetCategoryIcon(category);
        GameObject iconObject = CreateUiObject("Icon", buttonObject.transform, typeof(Image));
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = new Vector2(0.12f, 0.12f);
        iconRect.anchorMax = new Vector2(0.88f, 0.88f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = tabIcon;
        iconImage.enabled = tabIcon != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        button.onClick.AddListener(() => SelectCategory(category));
        CreativeHoverTooltipTarget tooltipTarget = buttonObject.AddComponent<CreativeHoverTooltipTarget>();
        tooltipTarget.Initialize(_hoverTooltip, category.CategoryName);
        return button;
    }

    private RectTransform CreateCategoryItemPage(Transform parent, ShopCategory category)
    {
        GameObject pageObject = CreateUiObject(
            "ItemsPage_" + CreativeNameUtils.SanitizeName(category.CategoryName, "Category"),
            parent,
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));

        RectTransform pageRect = (RectTransform)pageObject.transform;
        pageRect.anchorMin = new Vector2(0f, 1f);
        pageRect.anchorMax = new Vector2(1f, 1f);
        pageRect.pivot = new Vector2(0.5f, 1f);
        pageRect.anchoredPosition = Vector2.zero;
        pageRect.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup gridLayout = pageObject.GetComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(72f, 72f);
        gridLayout.spacing = new Vector2(6f, 6f);
        gridLayout.padding = new RectOffset(4, 4, 4, 4);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 8;

        ContentSizeFitter fitter = pageObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        foreach (ShopItem item in category.ShopItems)
        {
            if (!IsUsableShopItem(item))
            {
                continue;
            }

            CreativeInventoryItemButton.Create(pageRect, item, OnCreativeItemPressed, _hoverTooltip);
        }

        pageObject.SetActive(false);
        return pageRect;
    }

    private void SelectCategory(ShopCategory category)
    {
        if (category == null)
        {
            return;
        }

        _selectedCategory = category;
        int index = _categories.IndexOf(category);
        if (index >= 0)
        {
            _currentTabPage = index / _tabsPerPage;
        }

        RebuildCategoryTabs();
        ShowSelectedCategoryPage();
    }

    private void OnNextTabPagePressed()
    {
        int totalPages = GetTotalTabPages();
        if (totalPages <= 1)
        {
            return;
        }

        _currentTabPage++;
        if (_currentTabPage >= totalPages)
        {
            _currentTabPage = 0;
        }

        int firstIndex = _currentTabPage * _tabsPerPage;
        if (firstIndex < _categories.Count)
        {
            _selectedCategory = _categories[firstIndex];
        }

        RebuildCategoryTabs();
        ShowSelectedCategoryPage();
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            Button button = _tabButtons[i];
            if (button == null)
            {
                continue;
            }

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                continue;
            }

            ShopCategory category = FindCategoryForTab(button);
            bool selected = category != null && category == _selectedCategory;
            image.color = selected ? TabSelectedColor : TabNormalColor;
        }
    }

    private ShopCategory FindCategoryForTab(Button button)
    {
        foreach (KeyValuePair<ShopCategory, Button> pair in _categoryTabButtons)
        {
            if (pair.Value == button)
            {
                return pair.Key;
            }
        }

        return null;
    }

    private void ShowSelectedCategoryPage()
    {
        RectTransform selectedPage = null;

        foreach (KeyValuePair<ShopCategory, RectTransform> pair in _categoryItemPages)
        {
            RectTransform page = pair.Value;
            if (page == null)
            {
                continue;
            }

            bool isSelected = pair.Key == _selectedCategory;
            page.gameObject.SetActive(isSelected);
            if (isSelected)
            {
                selectedPage = page;
            }
        }

        _itemsGridRoot = selectedPage;
        _itemsGridLayout = selectedPage != null ? selectedPage.GetComponent<GridLayoutGroup>() : null;

        if (_itemsScrollRect != null)
        {
            _itemsScrollRect.content = selectedPage;
            _itemsScrollRect.verticalNormalizedPosition = 1f;
        }

        UpdateItemsGridLayout();
    }

    private bool IsInventoryVisible()
    {
        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        return inventoryUi != null && inventoryUi.IsShowingInventory();
    }

    private int GetTotalTabPages()
    {
        if (_categories.Count <= 0)
        {
            return 0;
        }

        return Mathf.CeilToInt(_categories.Count / (float)_tabsPerPage);
    }

    private void ClearCategoryTabs()
    {
        foreach (KeyValuePair<ShopCategory, Button> pair in _categoryTabButtons)
        {
            if (pair.Value != null)
            {
                Object.Destroy(pair.Value.gameObject);
            }
        }

        _categoryTabButtons.Clear();
        _tabButtons.Clear();
    }

    private void ClearItemButtons()
    {
        foreach (KeyValuePair<ShopCategory, RectTransform> pair in _categoryItemPages)
        {
            if (pair.Value != null)
            {
                Object.Destroy(pair.Value.gameObject);
            }
        }

        _categoryItemPages.Clear();
    }

    private void TearDownUi()
    {
        if (_nextTabPageButton != null)
        {
            _nextTabPageButton.onClick.RemoveListener(OnNextTabPagePressed);
        }

        ClearCategoryTabs();
        ClearItemButtons();

        if (_root != null)
        {
            Object.Destroy(_root);
            _root = null;
        }

        if (_overlayCanvasRoot != null)
        {
            Object.Destroy(_overlayCanvasRoot);
            _overlayCanvasRoot = null;
        }

        _frameRect = null;
        _rootBackgroundImage = null;
        _tabButtonsRoot = null;
        _itemsViewportRect = null;
        _itemsGridRoot = null;
        _itemsGridLayout = null;
        _itemsScrollRect = null;
        _nextTabPageButton = null;
        _tabPageText = null;
        _hoverTooltip = null;
        _selectedCategory = null;
        _categories.Clear();
        _suppressedNativeInventoryObjects.Clear();
        SetNativePreviewActive(true);
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] extraComponents)
    {
        System.Type[] types = new System.Type[extraComponents.Length + 1];
        types[0] = typeof(RectTransform);
        for (int i = 0; i < extraComponents.Length; i++)
        {
            types[i + 1] = extraComponents[i];
        }

        GameObject go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static bool IsUsableCategory(ShopCategory category)
    {
        if (category == null || category.ShopItems == null)
        {
            return false;
        }

        return category.ShopItems.Any(IsUsableShopItem);
    }

    private static bool IsUsableShopItem(ShopItem item)
    {
        if (item == null || item.Definition == null)
        {
            return false;
        }

        return !item.Definition.IsDummyItem;
    }

    private static int CountUsableItems(ShopCategory category)
    {
        if (category == null || category.ShopItems == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < category.ShopItems.Count; i++)
        {
            if (IsUsableShopItem(category.ShopItems[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static Sprite GetCategoryIcon(ShopCategory category)
    {
        if (category == null || category.ShopItems == null)
        {
            return null;
        }

        for (int i = 0; i < category.ShopItems.Count; i++)
        {
            ShopItem shopItem = category.ShopItems[i];
            if (!IsUsableShopItem(shopItem))
            {
                continue;
            }

            Sprite icon = shopItem.Definition.GetIcon();
            if (icon != null)
            {
                return icon;
            }
        }

        return null;
    }

}
