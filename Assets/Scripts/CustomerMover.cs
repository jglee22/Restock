using UnityEngine;
using UnityEngine.AI;

// 고객 한 명이 입장한 뒤, 진열된 상품 하나를 집어 나간다.
// 창고 재고는 건드리지 않고 Shelf.TryTakeOne만 호출한다.
public class CustomerMover : MonoBehaviour
{
    const float StuckTimeout = 2f;
    const float DestinationSampleRadius = 1f;

    enum CustomerState
    {
        Entering,
        SelectingProduct,
        MovingToShelf,
        Browsing,
        Leaving
    }

    NavMeshAgent agent;
    CustomerSpawner spawner;
    Transform insidePoint;
    Transform exitPoint;
    Shelf[] shoppingShelves;
    float browseDuration;
    CustomerState state;
    bool visitCompleted;
    bool moveFailed;
    bool hasWarned;

    public void Begin(
        CustomerSpawner owner,
        Transform inside,
        Transform exit,
        Shelf[] shelves,
        float browse)
    {
        spawner = owner;
        insidePoint = inside;
        exitPoint = exit;
        shoppingShelves = shelves;
        browseDuration = browse;
        agent = GetComponent<NavMeshAgent>();
        StartCoroutine(Visit());
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

        yield return WaitUntilArrived(insidePoint);
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
                    ? CustomerState.Leaving
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

                yield return WaitUntilArrived(chosenShelf.CustomerStandPoint);
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
                if (chosenShelf.TryTakeOne())
                {
                    state = CustomerState.Leaving;
                    continue;
                }

                state = BeginAnotherShelfAttempt(ref retried, ref excludedShelf, chosenShelf);
            }
        }

        state = CustomerState.Leaving;
        if (TrySetDestination(exitPoint))
        {
            yield return WaitUntilArrived(exitPoint);
        }

        FinishVisit();
    }

    CustomerState BeginAnotherShelfAttempt(ref bool retried, ref Shelf excludedShelf, Shelf failedShelf)
    {
        if (retried)
        {
            return CustomerState.Leaving;
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

    static bool CanShop(Shelf shelf, Shelf excludedShelf)
    {
        return shelf != null
            && shelf != excludedShelf
            && shelf.AssignedProduct != null
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

    System.Collections.IEnumerator WaitUntilArrived(Transform destination)
    {
        float stuckTime = 0f;
        string destinationName = destination != null ? destination.name : "목적지";

        while (!visitCompleted)
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

            if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
            {
                yield break;
            }

            if (agent.velocity.sqrMagnitude < 0.01f)
            {
                stuckTime += Time.deltaTime;
            }
            else
            {
                stuckTime = 0f;
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

    void FinishVisit()
    {
        if (visitCompleted)
        {
            return;
        }

        visitCompleted = true;
        if (spawner != null)
        {
            spawner.NotifyDeparted();
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
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
