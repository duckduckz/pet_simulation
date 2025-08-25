using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public KittyAgent agent;
    public Transform ballStart;    // assign in Inspector
    public Rigidbody ballRb;

    public bool endEpisodeOnGoal = true;    // keep true for training
    public bool respawnBallOnGoal = false;  // set true for free-play

    void Awake()
    {
        if (!ballRb && agent && agent.ball) ballRb = agent.ball.GetComponent<Rigidbody>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        if (endEpisodeOnGoal)
        {
            agent.ScoredGoal(); // resets via OnEpisodeBegin()
        }
        else if (respawnBallOnGoal && ballStart && ballRb)
        {
            ballRb.constraints = RigidbodyConstraints.None;
            other.transform.position = ballStart.position;
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
        }
    }
}
