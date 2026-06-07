using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class BasicUIManager
{
    private void BuildStoreComputerUI()
    {
        if (computerRoot == null)
        {
            GameObject rootObject = new GameObject("StoreComputerUI", typeof(RectTransform), typeof(Image));
            rootObject.transform.SetParent(canvasRoot, false);
            computerRoot = rootObject.GetComponent<RectTransform>();
        }

        computerRoot.anchorMin = Vector2.zero;
        computerRoot.anchorMax = Vector2.one;
        computerRoot.pivot = new Vector2(0.5f, 0.5f);
        computerRoot.offsetMin = Vector2.zero;
        computerRoot.offsetMax = Vector2.zero;

        Image background = computerRoot.GetComponent<Image>();
        if (background == null)
        {
            background = computerRoot.gameObject.AddComponent<Image>();
        }

        background.color = new Color(0.62f, 0.9f, 0.7f, 0.98f);
        background.raycastTarget = true;

        computerSidebarPanel = CreateComputerPanel("ComputerSidebar", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(270f, 0f), new Color(0.02f, 0.14f, 0.24f, 1f));
        computerSidebarPanel.anchorMin = new Vector2(0f, 0f);
        computerSidebarPanel.anchorMax = new Vector2(0f, 1f);
        computerSidebarPanel.pivot = new Vector2(0f, 0.5f);
        computerSidebarPanel.sizeDelta = new Vector2(270f, 0f);

        computerContentPanel = CreateComputerPanel("ComputerContent", new Vector2(0f, 0f), new Vector2(270f, 0f), new Vector2(-270f, 0f), new Color(0.86f, 0.94f, 0.96f, 1f));
        computerContentPanel.anchorMin = new Vector2(0f, 0f);
        computerContentPanel.anchorMax = new Vector2(1f, 1f);
        computerContentPanel.pivot = new Vector2(0.5f, 0.5f);
        computerContentPanel.offsetMin = new Vector2(270f, 0f);
        computerContentPanel.offsetMax = Vector2.zero;

        computerTitleText = CreateComputerLabel("ComputerTitle", computerSidebarPanel, new Vector2(22f, -24f), new Vector2(226f, 56f), 32f, "STORE OS");
        computerTitleText.color = TextColor;
        computerMarketTabButton = CreateComputerButton("ComputerMarketTab", "Market", ShowComputerMarket, computerSidebarPanel, new Vector2(22f, -110f), new Vector2(226f, 58f));
        computerPricingTabButton = CreateComputerButton("ComputerPricingTab", "Pricing", ShowComputerPricing, computerSidebarPanel, new Vector2(22f, -182f), new Vector2(226f, 58f));
        computerFurnitureTabButton = CreateComputerButton("ComputerFurnitureTab", "Furniture", ShowComputerFurniture, computerSidebarPanel, new Vector2(22f, -254f), new Vector2(226f, 58f));
        computerCloseButton = CreateComputerButton("ComputerCloseButton", "Close", CloseStoreComputer, computerSidebarPanel, new Vector2(22f, -340f), new Vector2(226f, 54f));

        computerStatusBarText = CreateComputerLabel("ComputerStatusBar", computerContentPanel, new Vector2(28f, -18f), new Vector2(1180f, 34f), 20f, string.Empty);
        computerStatusBarText.alignment = TextAlignmentOptions.TopRight;
        computerMessageText = CreateComputerLabel("ComputerMessage", computerContentPanel, new Vector2(28f, -996f), new Vector2(1180f, 44f), 21f, computerMessage);

        computerMarketPanel = CreateComputerContentPanel("ComputerMarketPanel");
        computerPricingPanel = CreateComputerContentPanel("ComputerPricingPanel");
        computerFurniturePanel = CreateComputerContentPanel("ComputerFurniturePanel");
        BuildComputerMarketPanel();
        BuildComputerPricingPanel();
        BuildComputerFurniturePanel();

        SetStoreComputerOpen(false);
    }

    private void BuildComputerMarketPanel()
    {
        AddComputerHeading(computerMarketPanel, "Market - Order Stock");
        computerSelectedProductText = CreateComputerLabel("ComputerSelectedProductText", computerMarketPanel, new Vector2(28f, -76f), new Vector2(420f, 118f), 23f, string.Empty);
        computerMarketStockText = CreateComputerLabel("ComputerMarketStockText", computerMarketPanel, new Vector2(486f, -76f), new Vector2(520f, 118f), 22f, string.Empty);
        computerOrderStockButton = CreateComputerButton("ComputerOrderStockButton", "Order Selected Stock", BuySelectedProductStockButton, computerMarketPanel, new Vector2(28f, -186f), new Vector2(300f, 46f));

        computerProductButtonPanel = FindDirectChildRect(computerMarketPanel, "ComputerProductButtons");
        if (computerProductButtonPanel == null)
        {
            GameObject panelObject = new GameObject("ComputerProductButtons", typeof(RectTransform));
            panelObject.transform.SetParent(computerMarketPanel, false);
            computerProductButtonPanel = panelObject.GetComponent<RectTransform>();
        }

        computerProductButtonPanel.anchorMin = new Vector2(0f, 1f);
        computerProductButtonPanel.anchorMax = new Vector2(0f, 1f);
        computerProductButtonPanel.pivot = new Vector2(0f, 1f);
        computerProductButtonPanel.anchoredPosition = new Vector2(28f, -258f);
        computerProductButtonPanel.sizeDelta = new Vector2(1040f, 610f);

        BuildComputerProductButtons();
    }

    private void BuildComputerFurniturePanel()
    {
        AddComputerHeading(computerFurniturePanel, "Furniture - Order Store Fixtures");
        computerFurnitureText = CreateComputerLabel(
            "ComputerFurnitureText",
            computerFurniturePanel,
            new Vector2(28f, -82f),
            new Vector2(880f, 150f),
            24f,
            "Store Shelf\nUnit Price: $50.00\nCustomer-facing shelf for products.\n\nWarehouse Rack\nUnit Price: $100.00\nHolds 4 physical stock boxes for delivered inventory.");
        computerBuyShelfButton = CreateComputerButton("ComputerBuyShelfButton", "Buy Store Shelf", BuyShelfButton, computerFurniturePanel, new Vector2(28f, -296f), new Vector2(260f, 46f));
        computerBuyWarehouseShelfButton = CreateComputerButton("ComputerBuyWarehouseShelfButton", "Buy Warehouse Rack", BuyWarehouseShelfButton, computerFurniturePanel, new Vector2(304f, -296f), new Vector2(300f, 46f));
    }

    private void BuildComputerPricingPanel()
    {
        AddComputerHeading(computerPricingPanel, "Pricing - Set Sale Prices");
        computerPricingText = CreateComputerLabel(
            "ComputerPricingText",
            computerPricingPanel,
            new Vector2(28f, -96f),
            new Vector2(980f, 250f),
            24f,
            string.Empty);

        computerPriceDownButton = CreateComputerButton("ComputerPriceDownButton", "- $0.25", DecreaseSelectedProductPriceButton, computerPricingPanel, new Vector2(28f, -370f), new Vector2(180f, 50f));
        computerPriceUpButton = CreateComputerButton("ComputerPriceUpButton", "+ $0.25", IncreaseSelectedProductPriceButton, computerPricingPanel, new Vector2(226f, -370f), new Vector2(180f, 50f));
        computerPriceResetButton = CreateComputerButton("ComputerPriceResetButton", "Reset Price", ResetSelectedProductPriceButton, computerPricingPanel, new Vector2(424f, -370f), new Vector2(220f, 50f));
    }

    private void BuildComputerProductButtons()
    {
        foreach (Button button in computerProductButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        computerProductButtons.Clear();

        float buttonWidth = 320f;
        float buttonHeight = 116f;
        float spacingX = 22f;
        float spacingY = 20f;
        int columns = 3;

        for (int index = 0; index < availableProducts.Count; index++)
        {
            ProductData product = availableProducts[index];
            if (product == null)
            {
                continue;
            }

            int column = index % columns;
            int row = index / columns;
            Vector2 position = new Vector2(column * (buttonWidth + spacingX), -(row * (buttonHeight + spacingY)));
            ProductData capturedProduct = product;
            Button button = CreateComputerButton(
                "ComputerProduct_" + product.productName,
                GetComputerProductCardText(product),
                () => SelectProduct(capturedProduct),
                computerProductButtonPanel,
                position,
                new Vector2(buttonWidth, buttonHeight));
            computerProductButtons.Add(button);
        }
    }

    private RectTransform CreateComputerPanel(string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        RectTransform panel = FindDirectChildRect(computerRoot, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(computerRoot, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = anchor;
        panel.anchorMax = anchor;
        panel.pivot = new Vector2(anchor.x, anchor.y);
        panel.anchoredPosition = anchoredPosition;
        panel.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private RectTransform CreateComputerContentPanel(string objectName)
    {
        RectTransform panel = FindDirectChildRect(computerContentPanel, objectName);
        if (panel == null)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform));
            panelObject.transform.SetParent(computerContentPanel, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        return panel;
    }

    private TMP_Text CreateComputerLabel(string objectName, RectTransform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, string text)
    {
        TMP_Text label = FindChildText(parent, objectName);
        if (label == null)
        {
            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(label);
        }

        ConfigureHudLabel(label, parent, anchoredPosition, size, fontSize, TextAlignmentOptions.TopLeft, DarkTextColor);
        label.text = text;
        return label;
    }

    private Button CreateComputerButton(
        string objectName,
        string labelText,
        UnityAction onClickAction,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Button button = FindButtonByName(objectName);
        if (button == null)
        {
            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        button.transform.SetParent(parent, false);
        button.onClick.RemoveAllListeners();
        if (onClickAction != null)
        {
            button.onClick.AddListener(onClickAction);
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        SetButtonLabel(button, labelText);
        StyleButton(button);
        return button;
    }

    private void SetStoreComputerOpen(bool isOpen)
    {
        isComputerOpen = isOpen;

        if (computerRoot != null)
        {
            computerRoot.gameObject.SetActive(isComputerOpen);
        }

        if (isComputerOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowComputerMarket();
            UpdateComputerTexts();
            UpdateStoreComputerButtonStates();
        }
    }

    private void ShowComputerPanel(RectTransform panelToShow)
    {
        activeComputerPanel = panelToShow;

        if (computerMarketPanel != null)
        {
            computerMarketPanel.gameObject.SetActive(panelToShow == computerMarketPanel);
        }

        if (computerPricingPanel != null)
        {
            computerPricingPanel.gameObject.SetActive(panelToShow == computerPricingPanel);
        }

        if (computerFurniturePanel != null)
        {
            computerFurniturePanel.gameObject.SetActive(panelToShow == computerFurniturePanel);
        }

        UpdateStoreComputerButtonStates();
    }

    private void SetComputerMessage(string message)
    {
        computerMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        if (computerMessageText != null)
        {
            computerMessageText.text = computerMessage;
        }
    }

    private void AddComputerHeading(RectTransform parent, string text)
    {
        TMP_Text heading = FindChildText(parent, "Heading");
        if (heading == null)
        {
            GameObject headingObject = new GameObject("Heading", typeof(RectTransform));
            headingObject.transform.SetParent(parent, false);
            heading = headingObject.AddComponent<TextMeshProUGUI>();
            CopyFontSettings(heading);
        }

        RectTransform rect = heading.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -42f);
        rect.sizeDelta = new Vector2(-24f, 26f);

        heading.text = text.ToUpperInvariant();
        heading.fontSize = 17f;
        heading.color = new Color(0.18f, 0.34f, 0.38f, 1f);
        heading.alignment = TextAlignmentOptions.TopLeft;
        heading.textWrappingMode = TextWrappingModes.NoWrap;
        heading.raycastTarget = false;
    }

    private void UpdateComputerProductButtonVisuals()
    {
        for (int index = 0; index < computerProductButtons.Count; index++)
        {
            Button button = computerProductButtons[index];
            if (button == null)
            {
                continue;
            }

            ProductData product = index < availableProducts.Count ? availableProducts[index] : null;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = product == selectedProduct ? SelectedProductColor : ButtonColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (product != null)
                {
                    label.text = GetComputerProductCardText(product);
                }

                label.color = DarkTextColor;
                label.fontStyle = product == selectedProduct ? FontStyles.Bold : FontStyles.Normal;
                label.alignment = TextAlignmentOptions.TopLeft;
                label.fontSize = 17f;
                label.textWrappingMode = TextWrappingModes.Normal;
            }
        }
    }

    private void UpdateComputerTexts()
    {
        if (computerStatusBarText != null)
        {
            string cashText = moneyManager != null ? "$" + moneyManager.CurrentMoney.ToString("0.00") : "$0.00";
            string dayTextValue = GameManager.Instance != null ? "Day " + GameManager.Instance.CurrentDay : "Day 1";
            string timeText = GameManager.Instance != null ? GameManager.Instance.StoreClockText : "8:00 AM";
            string stateText = GameManager.Instance != null ? GameManager.Instance.StoreStateText : "Preparing";
            computerStatusBarText.text = cashText + "    " + dayTextValue + "    " + timeText + "    " + stateText;
        }

        if (computerMessageText != null)
        {
            computerMessageText.text = computerMessage;
        }

        UpdateComputerPricingText();

        if (computerSelectedProductText == null || computerMarketStockText == null)
        {
            return;
        }

        if (selectedProduct == null)
        {
            computerSelectedProductText.text = "No product selected";
            computerMarketStockText.text = "Choose a product to order stock.";
            return;
        }

        int stock = GameManager.Instance != null ? GameManager.Instance.GetBackroomStock(selectedProduct) : 0;
        int incoming = GameManager.Instance != null ? GameManager.Instance.GetIncomingDeliveryAmount(selectedProduct) : 0;
        int atDock = GameManager.Instance != null ? GameManager.Instance.GetDockDeliveryAmount(selectedProduct) : 0;
        int shelfStock = GetStoreShelfStock(selectedProduct);
        float orderCost = selectedProduct.wholesaleCost * restockPurchaseAmount;
        float salePrice = GameManager.Instance != null
            ? GameManager.Instance.GetProductSalePrice(selectedProduct)
            : selectedProduct.price;
        float profit = salePrice - selectedProduct.wholesaleCost;

        computerSelectedProductText.text =
            "Selected Product\n" + selectedProduct.productName +
            "\nSale price: $" + salePrice.ToString("0.00") +
            "\nProfit/item: $" + profit.ToString("0.00") +
            "\nOrder " + restockPurchaseAmount + " for $" + orderCost.ToString("0.00");

        computerMarketStockText.text =
            "Inventory Status\nWarehouse shelf stock: " + stock +
            "\nOn store shelves: " + shelfStock +
            "\nAt loading dock: " + (atDock > 0 ? "+" + atDock : "None") +
            "\nIncoming delivery: " + (incoming > 0 ? "+" + incoming : "None");
    }

    private void UpdateComputerPricingText()
    {
        if (computerPricingText == null)
        {
            return;
        }

        if (selectedProduct == null)
        {
            computerPricingText.text = "No product selected\n\nSelect a product in the Market tab, then set its sale price here.";
            return;
        }

        float salePrice = GameManager.Instance != null
            ? GameManager.Instance.GetProductSalePrice(selectedProduct)
            : selectedProduct.price;
        float defaultPrice = selectedProduct.price;
        float wholesaleCost = selectedProduct.wholesaleCost;
        float profit = salePrice - wholesaleCost;
        float priceMultiplier = defaultPrice > 0f ? salePrice / defaultPrice : 1f;
        computerPricingText.text =
            "Selected Product\n" + selectedProduct.productName +
            "\n\nDefault price: $" + defaultPrice.ToString("0.00") +
            "\nCurrent sale price: $" + salePrice.ToString("0.00") +
            "\nWholesale cost: $" + wholesaleCost.ToString("0.00") +
            "\nProfit per item: $" + profit.ToString("0.00") +
            "\nPrice risk: " + GetPriceRiskLabel(priceMultiplier) +
            "\n\nCustomer feedback appears in the end-of-day report after closing.";
    }

    private string GetComputerProductCardText(ProductData product)
    {
        if (product == null)
        {
            return "Unknown Product";
        }

        int warehouseStock = GameManager.Instance != null ? GameManager.Instance.GetBackroomStock(product) : 0;
        int shelfStock = GetStoreShelfStock(product);
        int incoming = GameManager.Instance != null ? GameManager.Instance.GetIncomingDeliveryAmount(product) : 0;
        int atDock = GameManager.Instance != null ? GameManager.Instance.GetDockDeliveryAmount(product) : 0;
        float salePrice = GameManager.Instance != null ? GameManager.Instance.GetProductSalePrice(product) : product.price;
        float orderCost = product.wholesaleCost * restockPurchaseAmount;

        return product.productName +
               "\nSell $" + salePrice.ToString("0.00") + "  Cost $" + product.wholesaleCost.ToString("0.00") +
               "\nShelf " + shelfStock + "  Warehouse " + warehouseStock +
               "\nDock " + atDock + "  Incoming " + incoming +
               "\nOrder " + restockPurchaseAmount + " for $" + orderCost.ToString("0.00");
    }

    private int GetStoreShelfStock(ProductData product)
    {
        if (product == null)
        {
            return 0;
        }

        int total = 0;
        Shelf[] shelves = FindObjectsByType<Shelf>();
        foreach (Shelf shelf in shelves)
        {
            if (shelf != null && shelf.AssignedProduct == product)
            {
                total += shelf.CurrentStock;
            }
        }

        return total;
    }

    private string GetPriceRiskLabel(float priceMultiplier)
    {
        if (priceMultiplier >= 2f)
        {
            return "Too high - most customers refuse";
        }

        if (priceMultiplier >= 1.5f)
        {
            return "High - some customers complain";
        }

        if (priceMultiplier < 1f)
        {
            return "Discount - lower profit";
        }

        return "Normal";
    }

    private void UpdateStoreComputerButtonStates()
    {
        if (computerRoot == null || !computerRoot.gameObject.activeSelf)
        {
            return;
        }

        bool hasSelectedProduct = selectedProduct != null;
        bool canPlaceShelf = GameManager.Instance == null || !GameManager.Instance.IsPlacingShelf;

        SetButtonState(computerOrderStockButton, hasSelectedProduct);
        SetButtonState(computerPriceDownButton, hasSelectedProduct);
        SetButtonState(computerPriceUpButton, hasSelectedProduct);
        SetButtonState(computerPriceResetButton, hasSelectedProduct);
        SetButtonState(computerBuyShelfButton, canPlaceShelf);
        SetButtonState(computerBuyWarehouseShelfButton, canPlaceShelf);
        UpdateComputerTexts();
        UpdateComputerProductButtonVisuals();
        UpdateComputerTabVisuals();
    }

    private void UpdateComputerTabVisuals()
    {
        SetComputerTabVisual(computerMarketTabButton, activeComputerPanel == computerMarketPanel);
        SetComputerTabVisual(computerPricingTabButton, activeComputerPanel == computerPricingPanel);
        SetComputerTabVisual(computerFurnitureTabButton, activeComputerPanel == computerFurniturePanel);
    }

    private void SetComputerTabVisual(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = isActive ? SelectedProductColor : ButtonColor;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = DarkTextColor;
            label.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
