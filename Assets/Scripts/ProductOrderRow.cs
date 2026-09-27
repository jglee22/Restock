using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 주문 패널의 상품 한 줄을 표시하고, 버튼은 StoreOrdering에만 요청한다.
public class ProductOrderRow : MonoBehaviour
{
    [SerializeField] ProductDefinition product;
    [SerializeField] int orderQuantity = 1;
    [SerializeField] StoreOrdering ordering;
    [SerializeField] StoreInventory inventory;
    [SerializeField] StoreEconomy economy;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text stockText;
    [SerializeField] TMP_Text priceText;
    [SerializeField] TMP_Text buttonLabel;
    [SerializeField] Button orderButton;

    int shownStock = int.MinValue;
    int shownMoney = int.MinValue;

    void OnEnable()
    {
        if (orderButton != null)
        {
            orderButton.onClick.AddListener(Order);
        }
    }

    void Start()
    {
        Refresh();
    }

    void OnDisable()
    {
        if (orderButton != null)
        {
            orderButton.onClick.RemoveListener(Order);
        }
    }

    void Update()
    {
        if (inventory == null || economy == null || product == null)
        {
            return;
        }

        if (shownStock == inventory.GetQuantity(product) && shownMoney == economy.CurrentMoney)
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

        int stock = inventory != null ? inventory.GetQuantity(product) : 0;
        int money = economy != null ? economy.CurrentMoney : 0;
        shownStock = stock;
        shownMoney = money;

        SetText(nameText, product.DisplayName);
        SetText(stockText, "창고 " + stock.ToString(CultureInfo.InvariantCulture));
        SetText(priceText, "개당 " + FormatWon(product.PurchasePrice));
        SetText(buttonLabel, orderQuantity.ToString(CultureInfo.InvariantCulture) + "개 주문");

        if (orderButton != null && ordering != null)
        {
            bool canOrder = ordering.CanOrder(product, orderQuantity);
            if (orderButton.interactable != canOrder)
            {
                orderButton.interactable = canOrder;
            }
        }
    }

    void Order()
    {
        if (ordering == null || product == null)
        {
            return;
        }

        ordering.TryOrder(product, orderQuantity);
        Refresh();
    }

    void OnValidate()
    {
        if (orderQuantity < 1)
        {
            Debug.LogWarning($"ProductOrderRow: Order Quantity는 1 이상이어야 합니다. 현재 값: {orderQuantity}", this);
        }

        if (product == null)
        {
            Debug.LogWarning("ProductOrderRow: ProductDefinition이 연결되지 않았습니다.", this);
            return;
        }

        SetText(nameText, product.DisplayName);
        SetText(priceText, "개당 " + FormatWon(product.PurchasePrice));
        SetText(buttonLabel, orderQuantity.ToString(CultureInfo.InvariantCulture) + "개 주문");
    }

    static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value;
        }
    }

    static string FormatWon(int amount)
    {
        return "₩" + amount.ToString("N0", CultureInfo.InvariantCulture);
    }
}
