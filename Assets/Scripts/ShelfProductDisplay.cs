using UnityEngine;

// 진열대에 올라간 수량만 보여 준다.
// 창고 재고, 판매, 저장은 Shelf가 가진 값을 읽기만 한다.
public class ShelfProductDisplay : MonoBehaviour
{
    [SerializeField] Shelf shelf;
    [SerializeField] Transform[] slots;

    GameObject[] shown;
    ProductDefinition shownProduct;

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (shelf == null)
        {
            shelf = GetComponent<Shelf>();
        }

        if (shelf == null || slots == null || slots.Length == 0)
        {
            ClearShown();
            return;
        }

        ProductDefinition product = shelf.AssignedProduct;
        int quantity = shelf.CurrentQuantity;
        GameObject prefab = product != null ? product.ProductPrefab : null;
        if (product == null || prefab == null || quantity <= 0)
        {
            ClearShown();
            return;
        }

        if (shownProduct != product || shown == null || shown.Length != slots.Length)
        {
            ClearShown();
            Build(prefab);
            shownProduct = product;
        }

        int visibleCount = quantity < slots.Length ? quantity : slots.Length;
        for (int index = 0; index < shown.Length; index++)
        {
            if (shown[index] != null)
            {
                shown[index].SetActive(index < visibleCount);
            }
        }
    }

    void Build(GameObject prefab)
    {
        shown = new GameObject[slots.Length];
        for (int index = 0; index < slots.Length; index++)
        {
            Transform slot = slots[index];
            if (slot == null)
            {
                continue;
            }

            GameObject instance = Instantiate(prefab, slot);
            instance.name = prefab.name;
            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;
            shown[index] = instance;
        }
    }

    void ClearShown()
    {
        if (slots != null)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                Transform slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                for (int childIndex = slot.childCount - 1; childIndex >= 0; childIndex--)
                {
                    GameObject child = slot.GetChild(childIndex).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }
        }

        shown = null;
        shownProduct = null;
    }
}
