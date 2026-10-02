using UnityEngine;

// 진열대에 올라간 수량만 보여 준다.
// 창고 재고, 판매, 저장은 Shelf가 가진 값을 읽기만 한다.
public class ShelfProductDisplay : MonoBehaviour
{
    [SerializeField] Shelf shelf;
    [SerializeField] Transform[] slots;
    [SerializeField] Transform[] backSlots;

    GameObject[] shown;
    GameObject[] shownBack;
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

        bool backReady = backSlots == null || backSlots.Length == 0
            ? shownBack == null
            : shownBack != null && shownBack.Length == backSlots.Length;
        if (shownProduct != product || shown == null || shown.Length != slots.Length || !backReady)
        {
            ClearShown();
            shown = Build(prefab, slots);
            shownBack = Build(prefab, backSlots);
            shownProduct = product;
        }

        int visibleCount = quantity < slots.Length ? quantity : slots.Length;
        ApplyVisibility(shown, visibleCount);
        int backVisibleCount = backSlots == null || quantity < backSlots.Length ? quantity : backSlots.Length;
        if (backSlots == null)
        {
            backVisibleCount = 0;
        }

        ApplyVisibility(shownBack, backVisibleCount);
    }

    static void ApplyVisibility(GameObject[] instances, int visibleCount)
    {
        if (instances == null)
        {
            return;
        }

        for (int index = 0; index < instances.Length; index++)
        {
            if (instances[index] != null)
            {
                instances[index].SetActive(index < visibleCount);
            }
        }
    }

    static GameObject[] Build(GameObject prefab, Transform[] faceSlots)
    {
        if (faceSlots == null || faceSlots.Length == 0)
        {
            return null;
        }

        GameObject[] instances = new GameObject[faceSlots.Length];
        for (int index = 0; index < faceSlots.Length; index++)
        {
            Transform slot = faceSlots[index];
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
            instances[index] = instance;
        }

        return instances;
    }

    void ClearShown()
    {
        ClearSlotChildren(slots);
        ClearSlotChildren(backSlots);
        shown = null;
        shownBack = null;
        shownProduct = null;
    }

    static void ClearSlotChildren(Transform[] faceSlots)
    {
        if (faceSlots == null)
        {
            return;
        }

        for (int index = 0; index < faceSlots.Length; index++)
        {
            Transform slot = faceSlots[index];
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
}
