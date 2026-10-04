using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 가격 패널의 상품 한 줄을 표시하고, 버튼은 StorePricing에만 요청한다.
public class ProductPriceRow : MonoBehaviour
{
    [SerializeField] ProductDefinition product;
    [SerializeField] StorePricing pricing;
    [SerializeField] int priceStep = 100;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text basePriceText;
    [SerializeField] TMP_Text currentPriceText;
    [SerializeField] TMP_Text decreaseLabel;
    [SerializeField] TMP_Text increaseLabel;
    [SerializeField] Button decreaseButton;
    [SerializeField] Button increaseButton;

    public ProductDefinition Product => product;

    int shownPrice = int.MinValue;
    bool started;

    void OnEnable()
    {
        if (decreaseButton != null)
        {
            decreaseButton.onClick.AddListener(DecreasePrice);
        }

        if (increaseButton != null)
        {
            increaseButton.onClick.AddListener(IncreasePrice);
        }

        if (started)
        {
            Refresh();
        }
    }

    void Start()
    {
        started = true;
        Refresh();
    }

    void OnDisable()
    {
        if (decreaseButton != null)
        {
            decreaseButton.onClick.RemoveListener(DecreasePrice);
        }

        if (increaseButton != null)
        {
            increaseButton.onClick.RemoveListener(IncreasePrice);
        }
    }

    void Update()
    {
        if (pricing == null || product == null)
        {
            return;
        }

        if (!pricing.TryGetCurrentPrice(product, out int price) || price == shownPrice)
        {
            return;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (product == null)
        {
            return;
        }

        int currentPrice = product.BaseSellPrice;
        bool hasCurrentPrice = pricing != null && pricing.TryGetCurrentPrice(product, out currentPrice);
        if (!hasCurrentPrice)
        {
            currentPrice = product.BaseSellPrice;
        }

        shownPrice = hasCurrentPrice ? currentPrice : int.MinValue;
        SetText(nameText, product.DisplayName);
        SetText(basePriceText, "매입 " + FormatWon(product.PurchasePrice));
        SetText(currentPriceText, "판매 " + FormatWon(currentPrice));
        SetText(decreaseLabel, FormatStep(-priceStep));
        SetText(increaseLabel, FormatStep(priceStep));
        SetInteractable(decreaseButton, hasCurrentPrice && priceStep > 0 && currentPrice >= priceStep);
        SetInteractable(increaseButton, hasCurrentPrice && priceStep > 0 && currentPrice <= int.MaxValue - priceStep);
    }

    void DecreasePrice()
    {
        Adjust(-priceStep);
    }

    void IncreasePrice()
    {
        Adjust(priceStep);
    }

    void Adjust(int delta)
    {
        if (pricing == null || product == null || priceStep < 1)
        {
            return;
        }

        pricing.TryAdjustPrice(product, delta);
        Refresh();
    }

    void OnValidate()
    {
        if (priceStep < 1)
        {
            Debug.LogWarning($"ProductPriceRow: Price Step은 1 이상이어야 합니다. 현재 값: {priceStep}", this);
        }

        if (product == null)
        {
            Debug.LogWarning("ProductPriceRow: ProductDefinition이 연결되지 않았습니다.", this);
            return;
        }

        if (pricing == null)
        {
            Debug.LogWarning("ProductPriceRow: StorePricing이 연결되지 않았습니다.", this);
        }

        SetText(nameText, product.DisplayName);
        SetText(basePriceText, "매입 " + FormatWon(product.PurchasePrice));
        SetText(currentPriceText, "판매 " + FormatWon(product.BaseSellPrice));
        SetText(decreaseLabel, FormatStep(-priceStep));
        SetText(increaseLabel, FormatStep(priceStep));
    }

    static void SetInteractable(Button button, bool interactable)
    {
        if (button != null && button.interactable != interactable)
        {
            button.interactable = interactable;
        }
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    static string FormatStep(int step)
    {
        if (step > 0)
        {
            return "+" + step.ToString(CultureInfo.InvariantCulture);
        }

        return step.ToString(CultureInfo.InvariantCulture);
    }

    static string FormatWon(int amount)
    {
        return "₩" + amount.ToString("N0", CultureInfo.InvariantCulture);
    }
}
