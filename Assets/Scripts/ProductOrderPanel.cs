using UnityEngine;
using UnityEngine.UI;

// 주문 패널의 공통 수량을 고른다. 저장하지 않으며, 패널을 열면 1로 돌아간다.
public class ProductOrderPanel : MonoBehaviour
{
    [SerializeField] int selectedOrderQuantity = 1;
    [SerializeField] ProductDefinition[] products;
    [SerializeField] ProductOrderRow rowPrefab;
    [SerializeField] RectTransform rowContent;
    [SerializeField] StoreOrdering ordering;
    [SerializeField] StoreInventory inventory;
    [SerializeField] StoreEconomy economy;
    [SerializeField] ProductOrderRow[] rows;
    [SerializeField] Button quantityOneButton;
    [SerializeField] Button quantityTenButton;
    [SerializeField] Button quantityHundredButton;

    static readonly Color NormalColor = new Color(0.18f, 0.22f, 0.26f, 0.94f);
    static readonly Color SelectedColor = new Color(0.34f, 0.50f, 0.40f, 0.98f);

    public int SelectedOrderQuantity => selectedOrderQuantity;

    bool rowsBuilt;

    void OnEnable()
    {
        EnsureRows();
        selectedOrderQuantity = 1;
        Bind(quantityOneButton, SelectOne);
        Bind(quantityTenButton, SelectTen);
        Bind(quantityHundredButton, SelectHundred);
        ShowSelection();
        RefreshRows();
    }

    void EnsureRows()
    {
        if (products == null || products.Length == 0 || rowPrefab == null || rowContent == null)
        {
            return;
        }

        int unlockedCount = CountUnlockedProducts();
        if (rowsBuilt && rows != null && rows.Length == unlockedCount && (unlockedCount == 0 || rows[0] != null))
        {
            return;
        }

        for (int index = rowContent.childCount - 1; index >= 0; index--)
        {
            Destroy(rowContent.GetChild(index).gameObject);
        }

        rows = new ProductOrderRow[unlockedCount];
        int rowIndex = 0;
        for (int index = 0; index < products.Length; index++)
        {
            if (!IsUnlocked(products[index]))
            {
                continue;
            }

            ProductOrderRow row = Instantiate(rowPrefab, rowContent);
            row.name = products[index].name + "OrderRow";
            row.Bind(products[index], this, ordering, inventory, economy);
            rows[rowIndex] = row;
            rowIndex += 1;
        }

        rowsBuilt = true;
    }

    int CountUnlockedProducts()
    {
        int count = 0;
        for (int index = 0; index < products.Length; index++)
        {
            if (IsUnlocked(products[index]))
            {
                count += 1;
            }
        }

        return count;
    }

    static bool IsUnlocked(ProductDefinition product)
    {
        return product != null
            && (StoreProgression.Instance == null || StoreProgression.Instance.IsProductUnlocked(product));
    }

    void OnDisable()
    {
        Unbind(quantityOneButton, SelectOne);
        Unbind(quantityTenButton, SelectTen);
        Unbind(quantityHundredButton, SelectHundred);
    }

    void SelectOne()
    {
        SetQuantity(1);
    }

    void SelectTen()
    {
        SetQuantity(10);
    }

    void SelectHundred()
    {
        SetQuantity(100);
    }

    void SetQuantity(int quantity)
    {
        if (quantity != 1 && quantity != 10 && quantity != 100)
        {
            return;
        }

        selectedOrderQuantity = quantity;
        ShowSelection();
        RefreshRows();
    }

    void ShowSelection()
    {
        Paint(quantityOneButton, selectedOrderQuantity == 1);
        Paint(quantityTenButton, selectedOrderQuantity == 10);
        Paint(quantityHundredButton, selectedOrderQuantity == 100);
    }

    void RefreshRows()
    {
        if (rows == null)
        {
            return;
        }

        for (int index = 0; index < rows.Length; index++)
        {
            if (rows[index] != null)
            {
                rows[index].Refresh();
            }
        }
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        // 주문 버튼에서 복제되면서 비활성으로 저장되어, 포인터 클릭이 무시된다.
        button.interactable = true;
        button.onClick.AddListener(action);
    }

    static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    static void Paint(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color = selected ? SelectedColor : NormalColor;
    }
}
