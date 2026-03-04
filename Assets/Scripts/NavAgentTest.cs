using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NavAgentTest : MonoBehaviour
{
    public float roamRadius = 10f;
    public float minWaitTime = 0.5f;
    public float maxWaitTime = 2f;

    private NavMeshAgent agent;
    private float waitTimer;
    private float currentWaitTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        SetNewDestination();
    }

    void Update()
    {
        // If agent reached destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= currentWaitTime)
            {
                SetNewDestination();
                waitTimer = 0f;
            }
        }
    }

    void SetNewDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * roamRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            currentWaitTime = Random.Range(minWaitTime, maxWaitTime);
        }
    }
}

