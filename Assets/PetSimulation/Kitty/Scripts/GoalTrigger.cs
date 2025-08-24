using UnityEngine;
using Unity.MLAgents;

public class GoalTrigger : MonoBehaviour
{
    public KittyAgent agent;     

    void OnTriggerEnter(Collider other)
    {
        // Debug.LogError($"GOAL TRIGGER FIRED! Collided with object named '{other.name}' which has the tag '{other.tag}'.");
        if (other.CompareTag("Ball"))
        {
            agent.ScoredGoal();
        }
    }
}
