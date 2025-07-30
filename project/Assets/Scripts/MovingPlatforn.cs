using System;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private GameObject startPos;
    [SerializeField] private GameObject endPos;
    [SerializeField] private float speed = 1f;
    [SerializeField] private bool usePlayerTagOnly = false; // Option to only affect player
    
    private bool reachedToEnd = false;
    private float journeyTime = 0f;
    private float journeyLength;
    
    void Start()
    {
        // Calculate distance between positions
        if (startPos != null && endPos != null)
        {
            journeyLength = Vector3.Distance(startPos.transform.position, endPos.transform.position);
        }
    }
    
    void Update()
    {
        // Fixed null check (should be OR, not AND)
        if (startPos == null || endPos == null) return;

        // Calculate lerp parameter based on time and speed
        float distanceCovered = journeyTime * speed;
        float fractionOfJourney = distanceCovered / journeyLength;
        
        if (!reachedToEnd)
        {
            // Moving from start to end
            transform.position = Vector3.Lerp(startPos.transform.position, endPos.transform.position, fractionOfJourney);
            
            if (fractionOfJourney >= 1f)
            {
                reachedToEnd = true;
                journeyTime = 0f; // Reset journey time
            }
        }
        else
        {
            // Moving from end to start
            transform.position = Vector3.Lerp(endPos.transform.position, startPos.transform.position, fractionOfJourney);
            
            if (fractionOfJourney >= 1f)
            {
                reachedToEnd = false;
                journeyTime = 0f; // Reset journey time
            }
        }
        
        journeyTime += Time.deltaTime;
    }

  
}