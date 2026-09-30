using UnityEngine;
using UnityEngine.AI;

// 이동은 NavMeshAgent가 담당한다. Animator는 속도만 읽어 서 있는 자세와 걷는 자세를 바꾼다.
public class CustomerVisualAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");

    [SerializeField] NavMeshAgent agent;
    [SerializeField] Animator animator;

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (agent == null)
        {
            agent = GetComponentInParent<NavMeshAgent>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }

    void Update()
    {
        if (animator == null || agent == null)
        {
            return;
        }

        animator.SetFloat(SpeedId, agent.velocity.magnitude);
    }
}
