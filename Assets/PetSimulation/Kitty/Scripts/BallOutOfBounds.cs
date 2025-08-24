using UnityEngine;
#if UNITY_MLAGENTS_PRESENT
using Unity.MLAgents;
#endif

public class BallOutOfBounds : MonoBehaviour
{
	[Header("Reset Targets")]
	public Transform ballStart;
	public Transform agentStart;
#if UNITY_MLAGENTS_PRESENT
	public Agent agent;
#endif
	public GameEvents events;

	void OnCollisionEnter(Collision col)
	{
		if (!col.collider.CompareTag("Boundary")) return;

#if UNITY_MLAGENTS_PRESENT
		if (agent != null)
		{
			agent.AddReward(-1f);
			agent.EndEpisode();
		}
#endif
		if (events != null)
			events.OutOfBounds();

		ResetPositions();
	}

	void ResetPositions()
	{
		if (ballStart != null)
		{
			transform.position = ballStart.position;
			Rigidbody rb = GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}

#if UNITY_MLAGENTS_PRESENT
		if (agent != null && agentStart != null)
		{
			agent.transform.position = agentStart.position;
			Rigidbody agentRb = agent.GetComponent<Rigidbody>();
			if (agentRb != null)
			{
				agentRb.velocity = Vector3.zero;
				agentRb.angularVelocity = Vector3.zero;
			}
		}
#endif
	}
}

public interface GameEvents
{
	void OutOfBounds();
}