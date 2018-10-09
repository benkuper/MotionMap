using UnityEngine;
using System.Collections;
using DG.Tweening;

public class MotionMapCursor : MonoBehaviour {

    public int clusterID;
    public Vector3 clusterCenter;
    public Vector3 clusterOrientation;

    Color color;

	// Use this for initialization
	void Start () {
    }
	
	// Update is called once per frame
	void Update () {

    }

    public void update(Vector3 center, Vector3 orientation)
    {

        clusterCenter = center;
        clusterOrientation = orientation;
    }

    public void setColor(Color c)
    {
        color = c;
        GetComponent<Renderer>().material.color = c;
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(clusterCenter,Vector3.one*.05f);
        Gizmos.DrawRay(clusterCenter, clusterOrientation);
       // Gizmos.DrawLine(clusterCenter, clusterCenter + clusterOrientation);
        Gizmos.DrawWireSphere(transform.position, .2f);
        
    }
}
