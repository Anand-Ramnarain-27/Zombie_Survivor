using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnBlocker : MonoBehaviour
{
    public Material failMat, succMat;
    public bool block;
    public Spawner spawner;
    private void OnTriggerEnter(Collider other)
    {
        if(block)
            DenySpawn(other);
        else 
            OkForSpawn(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if(block)
            OkForSpawn(other);
        else
            DenySpawn(other);
    }
    private void OkForSpawn(Collider other)
    {
#if UNITY_EDITOR
        other.GetComponent<MeshRenderer>().material = succMat;
#endif
        if (!spawner.spawnZones.Contains(other))
            spawner.spawnZones.Add(other);
    }

    private void DenySpawn(Collider other)
    {
#if UNITY_EDITOR
        other.GetComponent<MeshRenderer>().material = failMat;
#endif
        if (spawner.spawnZones.Contains(other))
            spawner.spawnZones.Remove(other);
    }

}
