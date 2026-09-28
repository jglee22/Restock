using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 계산대 하나의 줄을 순서대로 진행하고, 계산이 끝난 판매만 기록한다.
// 재고는 바꾸지 않는다.
public class CheckoutCounter : MonoBehaviour
{
    [SerializeField] Transform[] queuePoints;
    [SerializeField] StoreEconomy economy;
    [SerializeField] StoreStatistics statistics;
    [SerializeField] float checkoutDuration = 1.5f;

    readonly List<CustomerMover> queue = new List<CustomerMover>();
    bool checkoutInProgress;
    int checkoutToken;
    bool hasWarned;

    public bool TryEnqueue(CustomerMover customer)
    {
        if (customer == null)
        {
            WarnOnce("CheckoutCounter: 줄에 넣을 고객이 없습니다.");
            return false;
        }

        if (!HasQueuePoints())
        {
            WarnOnce("CheckoutCounter: Queue Point가 없습니다.");
            return false;
        }

        if (queue.Contains(customer))
        {
            return true;
        }

        if (queue.Count >= queuePoints.Length)
        {
            WarnOnce("CheckoutCounter: 빈 Queue Point가 없어 고객이 줄에 들어가지 못했습니다.");
            return false;
        }

        queue.Add(customer);
        UpdateQueueTargets();
        return true;
    }

    public bool TryBeginCheckout(CustomerMover customer)
    {
        if (checkoutInProgress || customer == null || queue.Count == 0 || queue[0] != customer)
        {
            return false;
        }

        checkoutInProgress = true;
        int token = checkoutToken;
        customer.NotifyCheckoutStarted();
        StartCoroutine(FinishCheckout(customer, token));
        return true;
    }

    public void ReleaseCustomer(CustomerMover customer)
    {
        int index = queue.IndexOf(customer);
        if (index < 0)
        {
            return;
        }

        queue.RemoveAt(index);
        if (index == 0 && checkoutInProgress)
        {
            checkoutInProgress = false;
            checkoutToken += 1;
        }

        UpdateQueueTargets();
    }

    IEnumerator FinishCheckout(CustomerMover customer, int token)
    {
        yield return new WaitForSeconds(checkoutDuration);
        if (token != checkoutToken)
        {
            yield break;
        }

        if (queue.Count > 0 && queue[0] == customer)
        {
            queue.RemoveAt(0);
        }

        checkoutInProgress = false;
        UpdateQueueTargets();
        if (customer != null)
        {
            RecordCompletedSale(customer);
            customer.NotifyCheckoutCompleted();
        }
    }

    void RecordCompletedSale(CustomerMover customer)
    {
        IReadOnlyList<CustomerBasketItem> items = customer.BasketItems;
        if (items == null || items.Count == 0)
        {
            WarnOnce("CheckoutCounter: 계산을 마친 고객의 장바구니가 비어 있어 매출을 기록하지 않습니다.");
            return;
        }

        if (economy == null)
        {
            WarnOnce("CheckoutCounter: StoreEconomy가 연결되지 않아 매출을 기록하지 못했습니다.");
            return;
        }

        for (int index = 0; index < items.Count; index++)
        {
            CustomerBasketItem item = items[index];
            if (item.Product == null || item.UnitPrice < 0)
            {
                WarnOnce("CheckoutCounter: 장바구니에 판매할 수 없는 상품이 있어 매출을 기록하지 않습니다.");
                return;
            }
        }

        for (int index = 0; index < items.Count; index++)
        {
            CustomerBasketItem item = items[index];
            economy.RecordSale(item.Product, item.UnitPrice);
        }

        if (statistics == null)
        {
            WarnOnce("CheckoutCounter: StoreStatistics가 연결되지 않아 구매 통계를 기록하지 못했습니다.");
            return;
        }

        statistics.RecordCompletedPurchase(items);
    }

    void UpdateQueueTargets()
    {
        for (int index = 0; index < queue.Count; index++)
        {
            CustomerMover customer = queue[index];
            Transform point = queuePoints[index];
            if (customer == null || point == null)
            {
                continue;
            }

            customer.SetCheckoutQueueTarget(point, index == 0);
        }
    }

    bool HasQueuePoints()
    {
        if (queuePoints == null || queuePoints.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < queuePoints.Length; index++)
        {
            if (queuePoints[index] == null)
            {
                return false;
            }
        }

        return true;
    }

    void OnValidate()
    {
        if (checkoutDuration < 0f)
        {
            Debug.LogWarning($"CheckoutCounter: Checkout Duration은 0 이상이어야 합니다. 현재 값: {checkoutDuration}", this);
        }

        if (queuePoints == null)
        {
            return;
        }

        for (int index = 0; index < queuePoints.Length; index++)
        {
            if (queuePoints[index] == null)
            {
                Debug.LogWarning($"CheckoutCounter: Queue Point {index}가 비어 있습니다.", this);
            }
        }
    }

    void WarnOnce(string message)
    {
        if (hasWarned)
        {
            return;
        }

        hasWarned = true;
        Debug.LogWarning(message, this);
    }
}
