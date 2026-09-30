using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public readonly struct CustomerBasketItem
{
    public CustomerBasketItem(ProductDefinition product, int unitPrice)
    {
        Product = product;
        UnitPrice = unitPrice;
    }

    public ProductDefinition Product { get; }
    public int UnitPrice { get; }
}

// 고객 한 명이 서로 다른 상품을 장바구니에 담은 뒤 한 번 계산하고 나간다.
// 창고 재고는 건드리지 않고, 계산대에서 Shelf 수량을 다시 줄이지 않는다.
public class CustomerMover : MonoBehaviour
{
    const float StuckTimeout = 2f;
    const float DestinationSampleRadius = 1f;
    const float ArrivalSlack = 0.15f;
    const float CertainPurchasePriceRatio = 0.5f;
    const float RejectPurchasePriceRatio = 2f;

    enum CustomerState
    {
        Entering,
        SelectingProduct,
        MovingToShelf,
        Browsing,
        MovingToCheckout,
        WaitingCheckout,
        CheckingOut,
        Leaving
    }

    NavMeshAgent agent;
    CustomerSpawner spawner;
    Transform insidePoint;
    Transform exitPoint;
    Shelf[] shoppingShelves;
    CheckoutCounter checkout;
    StorePricing pricing;
    StoreEventSystem eventSystem;
    StoreUpgradeSystem upgradeSystem;
    CustomerDefinition customerDefinition;
    int remainingBudget;
    float browseDuration;
    readonly List<CustomerBasketItem> basket = new List<CustomerBasketItem>();
    readonly HashSet<ProductDefinition> attemptedProducts = new HashSet<ProductDefinition>();
    // 같은 고객이 같은 상품의 품절을 반복 집계하지 않도록 방문 단위로 기억한다.
    readonly HashSet<ProductDefinition> recordedStockouts = new HashSet<ProductDefinition>();
    StoreStatistics statistics;
    bool missingStatisticsWarned;
    int targetItemCount;
    Transform checkoutQueueTarget;
    bool checkoutTargetDirty;
    bool arrivedAtCheckoutTarget;
    bool isCheckoutFront;
    bool checkoutFinished;
    CustomerState state;
    bool visitCompleted;
    bool moveFailed;
    bool hasWarned;

    public IReadOnlyList<CustomerBasketItem> BasketItems => basket;

    public int BasketCount => basket.Count;

    public CustomerDefinition Definition => customerDefinition;

    public int RemainingBudget => remainingBudget;

    public void Begin(
        CustomerSpawner owner,
        Transform inside,
        Transform exit,
        Shelf[] shelves,
        float browse,
        CheckoutCounter checkoutCounter,
        StorePricing storePricing,
        CustomerDefinition definition,
        StoreEventSystem storeEventSystem,
        StoreUpgradeSystem storeUpgradeSystem)
    {
        spawner = owner;
        insidePoint = inside;
        exitPoint = exit;
        shoppingShelves = shelves;
        checkout = checkoutCounter;
        pricing = storePricing;
        eventSystem = storeEventSystem;
        upgradeSystem = storeUpgradeSystem;
        customerDefinition = definition;
        remainingBudget = definition.Budget;
        browseDuration = browse * definition.BrowseTimeModifier;
        basket.Clear();
        attemptedProducts.Clear();
        recordedStockouts.Clear();
        statistics = Object.FindAnyObjectByType<StoreStatistics>();
        targetItemCount = NextTargetItemCount();
        agent = GetComponent<NavMeshAgent>();
        StartCoroutine(Visit());
    }

    public void SetCheckoutQueueTarget(Transform target, bool isFront)
    {
        if (state == CustomerState.CheckingOut || checkoutFinished)
        {
            return;
        }

        isCheckoutFront = isFront;
        if (checkoutQueueTarget == target)
        {
            return;
        }

        checkoutQueueTarget = target;
        checkoutTargetDirty = true;
        arrivedAtCheckoutTarget = false;
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    public void NotifyCheckoutStarted()
    {
        state = CustomerState.CheckingOut;
        if (agent == null)
        {
            return;
        }

        agent.ResetPath();
        agent.isStopped = true;
        agent.updateRotation = false;
        FaceCheckoutCounter();
    }

    public void NotifyCheckoutCompleted()
    {
        checkoutFinished = true;
        state = CustomerState.Leaving;
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
        }
    }

    void FaceCheckoutCounter()
    {
        if (checkout == null)
        {
            return;
        }

        Vector3 direction = checkout.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction);
    }

    System.Collections.IEnumerator Visit()
    {
        state = CustomerState.Entering;
        if (agent == null)
        {
            WarnOnce("CustomerMover: NavMeshAgent가 없습니다.");
            FinishVisit();
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            WarnOnce("CustomerMover: NavMesh 위에 있지 않아 이동할 수 없습니다.");
            FinishVisit();
            yield break;
        }

        if (!TrySetDestination(insidePoint))
        {
            FinishVisit();
            yield break;
        }

        yield return WaitUntilArrived(insidePoint, acceptNearbyStop: true);
        if (moveFailed)
        {
            FinishVisit();
            yield break;
        }

        Shelf excludedShelf = null;
        Shelf chosenShelf = null;
        bool retried = false;
        state = CustomerState.SelectingProduct;

        while (state != CustomerState.Leaving && !visitCompleted)
        {
            if (state == CustomerState.SelectingProduct)
            {
                chosenShelf = ChooseShelf(excludedShelf);
                state = chosenShelf == null
                    ? FinishShopping()
                    : CustomerState.MovingToShelf;
                continue;
            }

            if (state == CustomerState.MovingToShelf)
            {
                if (!TrySetDestination(chosenShelf.CustomerStandPoint))
                {
                    state = BeginAnotherShelfAttempt(ref retried, ref excludedShelf, chosenShelf);
                    continue;
                }

                yield return WaitUntilArrived(chosenShelf.CustomerStandPoint, acceptNearbyStop: true);
                if (moveFailed)
                {
                    state = BeginAnotherShelfAttempt(ref retried, ref excludedShelf, chosenShelf);
                    continue;
                }

                state = CustomerState.Browsing;
                continue;
            }

            if (state == CustomerState.Browsing)
            {
                yield return new WaitForSeconds(browseDuration);
                ProductDefinition browsedProduct = chosenShelf.AssignedProduct;
                if (browsedProduct != null)
                {
                    attemptedProducts.Add(browsedProduct);
                    if (WantsToBuy(browsedProduct, out int unitPrice) && chosenShelf.TryTakeOne() && !BasketContains(browsedProduct))
                    {
                        basket.Add(new CustomerBasketItem(browsedProduct, unitPrice));
                        // 구매 확률을 통과한 시점이 아니라, 진열에서 꺼내 바구니에 넣은 뒤에만 예산을 쓴다.
                        remainingBudget -= unitPrice;
                    }
                }

                retried = false;
                excludedShelf = null;
                state = basket.Count >= targetItemCount
                    ? BeginCheckout()
                    : CustomerState.SelectingProduct;
                continue;
            }

            if (state == CustomerState.CheckingOut)
            {
                // ResetPath가 같은 프레임에 정지를 해제하므로, 계산이 끝날 때까지 멈춰 둔다.
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.updateRotation = false;
                    if (agent.velocity.sqrMagnitude > 0.01f)
                    {
                        agent.velocity = Vector3.zero;
                    }
                }

                yield return null;
                continue;
            }

            if (state == CustomerState.MovingToCheckout || state == CustomerState.WaitingCheckout)
            {
                if (checkoutTargetDirty)
                {
                    state = CustomerState.MovingToCheckout;
                    arrivedAtCheckoutTarget = false;
                    if (!TrySetDestination(checkoutQueueTarget))
                    {
                        state = CustomerState.Leaving;
                        continue;
                    }

                    checkoutTargetDirty = false;
                }

                if (!arrivedAtCheckoutTarget)
                {
                    yield return WaitUntilArrived(checkoutQueueTarget);
                    if (checkoutTargetDirty)
                    {
                        continue;
                    }

                    if (moveFailed)
                    {
                        state = CustomerState.Leaving;
                        continue;
                    }

                    arrivedAtCheckoutTarget = true;
                }

                if (agent != null)
                {
                    agent.isStopped = true;
                }

                state = CustomerState.WaitingCheckout;
                if (isCheckoutFront && checkout != null)
                {
                    checkout.TryBeginCheckout(this);
                }

                yield return null;
            }
        }

        checkoutTargetDirty = false;
        if (agent != null)
        {
            agent.isStopped = false;
            // 계산을 마친 고객이 줄 맨 앞에 멈춰 있으면 다음 고객이 그 자리에 도착하지 못한다.
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        if (checkout != null)
        {
            checkout.ReleaseCustomer(this);
        }

        state = CustomerState.Leaving;
        if (TrySetDestination(exitPoint))
        {
            yield return WaitUntilArrived(exitPoint, acceptNearbyStop: true);
        }

        FinishVisit();
    }

    int NextTargetItemCount()
    {
        int minimum = customerDefinition.MinTargetItems;
        int maximum = customerDefinition.MaxTargetItems;
        if (maximum == int.MaxValue)
        {
            return maximum;
        }

        return Random.Range(minimum, maximum + 1);
    }

    bool BasketContains(ProductDefinition product)
    {
        for (int index = 0; index < basket.Count; index++)
        {
            if (basket[index].Product == product)
            {
                return true;
            }
        }

        return false;
    }

    CustomerState FinishShopping()
    {
        if (basket.Count > 0)
        {
            return BeginCheckout();
        }

        return CustomerState.Leaving;
    }

    bool WantsToBuy(ProductDefinition product, out int unitPrice)
    {
        unitPrice = 0;
        if (pricing == null)
        {
            WarnOnce("CustomerMover: StorePricing이 연결되지 않아 구매를 판단할 수 없습니다.");
            return false;
        }

        if (!pricing.TryGetCurrentPrice(product, out unitPrice))
        {
            WarnOnce($"CustomerMover: {product.DisplayName}의 현재 판매 가격이 없습니다.");
            return false;
        }

        if (product.BaseSellPrice <= 0)
        {
            WarnOnce($"CustomerMover: {product.DisplayName}의 Base Sell Price가 0 이하라 구매 확률을 계산할 수 없습니다.");
            return false;
        }

        if (unitPrice > remainingBudget)
        {
            return false;
        }

        float chance = CalculatePurchaseChance(
            unitPrice,
            product.BaseSellPrice,
            product.Popularity,
            customerDefinition.PriceSensitivity);
        float eventMultiplier = eventSystem != null ? eventSystem.GetPurchaseChanceMultiplier(product) : 1f;
        float upgradeMultiplier = upgradeSystem != null ? upgradeSystem.PurchaseChanceMultiplier : 1f;
        return Random.value < Mathf.Clamp01(chance * eventMultiplier * upgradeMultiplier);
    }

    static float CalculatePurchaseChance(int currentPrice, int basePrice, float popularity, float priceSensitivity)
    {
        float ratio = currentPrice / (float)basePrice;
        float chance;
        if (ratio <= CertainPurchasePriceRatio)
        {
            chance = 1f;
        }
        else if (ratio >= RejectPurchasePriceRatio)
        {
            // 기준가 2배 이상은 민감도와 상관없이 구매하지 않는다.
            chance = 0f;
        }
        else if (ratio <= 1f)
        {
            float t = (ratio - CertainPurchasePriceRatio) / (1f - CertainPurchasePriceRatio);
            chance = Mathf.Lerp(1f, popularity, t);
        }
        else
        {
            // 기준가보다 비싼 구간에서만 민감도가 거절 속도를 바꾼다.
            float t = (ratio - 1f) / (RejectPurchasePriceRatio - 1f);
            float sensitiveT = Mathf.Clamp01(t * priceSensitivity);
            chance = Mathf.Lerp(popularity, 0f, sensitiveT);
        }

        return Mathf.Clamp01(chance);
    }

    CustomerState BeginCheckout()
    {
        if (basket.Count <= 0)
        {
            return CustomerState.Leaving;
        }

        if (checkout != null && checkout.TryEnqueue(this))
        {
            return CustomerState.MovingToCheckout;
        }

        WarnOnce("CustomerMover: 계산대 줄에 들어가지 못해 퇴장합니다.");
        return CustomerState.Leaving;
    }

    CustomerState BeginAnotherShelfAttempt(ref bool retried, ref Shelf excludedShelf, Shelf failedShelf)
    {
        if (retried)
        {
            return FinishShopping();
        }

        retried = true;
        excludedShelf = failedShelf;
        return CustomerState.SelectingProduct;
    }

    Shelf ChooseShelf(Shelf excludedShelf)
    {
        if (shoppingShelves == null)
        {
            WarnOnce("CustomerMover: 쇼핑 Shelf 목록이 없습니다.");
            return null;
        }

        ObserveStockouts();

        int candidateCount = 0;
        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            if (CanShop(shoppingShelves[index], excludedShelf))
            {
                candidateCount += 1;
            }
        }

        if (candidateCount == 0)
        {
            return null;
        }

        int pick = Random.Range(0, candidateCount);
        int seen = 0;
        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            Shelf shelf = shoppingShelves[index];
            if (!CanShop(shelf, excludedShelf))
            {
                continue;
            }

            if (seen == pick)
            {
                return shelf;
            }

            seen += 1;
        }

        return null;
    }

    void ObserveStockouts()
    {
        for (int index = 0; index < shoppingShelves.Length; index++)
        {
            Shelf shelf = shoppingShelves[index];
            ProductDefinition product = shelf != null ? shelf.AssignedProduct : null;
            if (product == null || attemptedProducts.Contains(product) || recordedStockouts.Contains(product))
            {
                continue;
            }

            bool assigned = false;
            bool hasStock = false;
            for (int otherIndex = 0; otherIndex < shoppingShelves.Length; otherIndex++)
            {
                Shelf other = shoppingShelves[otherIndex];
                if (other == null || other.AssignedProduct != product)
                {
                    continue;
                }

                assigned = true;
                if (other.CurrentQuantity > 0)
                {
                    hasStock = true;
                    break;
                }
            }

            if (!assigned || hasStock)
            {
                continue;
            }

            recordedStockouts.Add(product);
            if (statistics == null)
            {
                if (!missingStatisticsWarned)
                {
                    missingStatisticsWarned = true;
                    Debug.LogWarning("CustomerMover: StoreStatistics가 없어 품절을 기록하지 않습니다.", this);
                }

                continue;
            }

            statistics.RecordStockout();
        }
    }

    bool CanShop(Shelf shelf, Shelf excludedShelf)
    {
        return shelf != null
            && shelf != excludedShelf
            && shelf.AssignedProduct != null
            && !attemptedProducts.Contains(shelf.AssignedProduct)
            && shelf.CurrentQuantity > 0
            && !shelf.IsEmpty
            && shelf.CustomerStandPoint != null;
    }

    bool TrySetDestination(Transform destination)
    {
        moveFailed = false;

        if (destination == null)
        {
            WarnOnce("CustomerMover: 이동 지점이 연결되지 않았습니다.");
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            WarnOnce("CustomerMover: NavMesh 위에 있지 않아 목적지를 설정할 수 없습니다.");
            return false;
        }

        if (!NavMesh.SamplePosition(destination.position, out NavMeshHit hit, DestinationSampleRadius, NavMesh.AllAreas))
        {
            WarnOnce($"CustomerMover: {destination.name} 근처에서 NavMesh를 찾지 못했습니다.");
            return false;
        }

        if (agent.SetDestination(hit.position))
        {
            return true;
        }

        WarnOnce($"CustomerMover: 목적지 설정에 실패했습니다. {destination.name}");
        return false;
    }

    System.Collections.IEnumerator WaitUntilArrived(Transform destination, bool acceptNearbyStop = false)
    {
        float stuckTime = 0f;
        float closestPlanarDistance = float.PositiveInfinity;
        string destinationName = destination != null ? destination.name : "목적지";
        float exactArrivalDistance = agent.stoppingDistance + ArrivalSlack;
        // 같은 지점에 서 있는 다른 고객 한 명 너머까지는 도착으로 본다.
        float blockedArrivalDistance = exactArrivalDistance + agent.radius * 2f;

        while (!visitCompleted && !checkoutTargetDirty)
        {
            if (agent.pathPending)
            {
                yield return null;
                continue;
            }

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                moveFailed = true;
                WarnOnce($"CustomerMover: 경로를 찾지 못했습니다. {destinationName}");
                yield break;
            }

            float planarDistance = PlanarDistance(transform.position, destination.position);
            if (planarDistance <= exactArrivalDistance
                || (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance))
            {
                yield break;
            }

            if (acceptNearbyStop
                && planarDistance <= blockedArrivalDistance
                && agent.velocity.sqrMagnitude < 0.01f)
            {
                yield break;
            }

            // 계산대 자리는 앞 고객이 비킬 때까지 기다린다. 2초 만에 포기하면 계산이 취소된다.
            if (!acceptNearbyStop)
            {
                yield return null;
                continue;
            }

            if (planarDistance < closestPlanarDistance - 0.05f)
            {
                closestPlanarDistance = planarDistance;
                stuckTime = 0f;
            }
            else if (agent.velocity.sqrMagnitude < 0.01f)
            {
                stuckTime += Time.deltaTime;
            }

            if (stuckTime >= StuckTimeout)
            {
                moveFailed = true;
                WarnOnce($"CustomerMover: 목적지에 도착하지 못했습니다. {destinationName}");
                yield break;
            }

            yield return null;
        }
    }

    static float PlanarDistance(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to);
    }

    void FinishVisit()
    {
        if (visitCompleted)
        {
            return;
        }

        visitCompleted = true;
        if (checkout != null)
        {
            checkout.ReleaseCustomer(this);
        }

        if (spawner != null)
        {
            spawner.NotifyDeparted();
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (checkout != null)
        {
            checkout.ReleaseCustomer(this);
        }

        if (visitCompleted || spawner == null)
        {
            return;
        }

        visitCompleted = true;
        spawner.NotifyDeparted();
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
