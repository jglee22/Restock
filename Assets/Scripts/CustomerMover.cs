using UnityEngine;
using UnityEngine.AI;

// 고객 한 명이 매장 안쪽까지 갔다가 잠시 머물고 출구로 나간다.
// 상품, 진열대, 계산은 다루지 않는다.
public class CustomerMover : MonoBehaviour
{
    const float StuckTimeout = 2f;

    NavMeshAgent agent;
    CustomerSpawner spawner;
    Transform insidePoint;
    Transform exitPoint;
    float stayDuration;
    bool visitCompleted;
    bool hasWarned;

    public void Begin(CustomerSpawner owner, Transform inside, Transform exit, float stay)
    {
        spawner = owner;
        insidePoint = inside;
        exitPoint = exit;
        stayDuration = stay;
        agent = GetComponent<NavMeshAgent>();
        StartCoroutine(Visit());
    }

    System.Collections.IEnumerator Visit()
    {
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
        if (!visitCompleted && hasWarned)
        {
            FinishVisit();
            yield break;
        }

        yield return new WaitForSeconds(stayDuration);

        if (!TrySetDestination(exitPoint))
        {
            FinishVisit();
            yield break;
        }

        yield return WaitUntilArrived(exitPoint);
        FinishVisit();
    }

    bool TrySetDestination(Transform destination)
    {
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

        if (agent.SetDestination(destination.position))
        {
            return true;
        }

        WarnOnce($"CustomerMover: 목적지 설정에 실패했습니다. {destination.name}");
        return false;
    }

    System.Collections.IEnumerator WaitUntilArrived(Transform destination)
    {
        float stuckTime = 0f;

        while (!visitCompleted)
        {
            if (agent.pathPending)
            {
                yield return null;
                continue;
            }

            if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                WarnOnce($"CustomerMover: 경로를 찾지 못했습니다. {destination.name}");
                yield break;
            }

            if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
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
                WarnOnce($"CustomerMover: 목적지에 도착하지 못했습니다. {destination.name}");
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
