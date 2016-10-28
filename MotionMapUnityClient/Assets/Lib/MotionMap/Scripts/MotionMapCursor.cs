using UnityEngine;
using System.Collections;

public class MotionMapCursor : MonoBehaviour {

    public Cluster c;

    int plateauLayer;

	// Use this for initialization
	void Start () {
	    plateauLayer = LayerMask.NameToLayer("plateau");
    }
	
	// Update is called once per frame
	void Update () {
        RaycastHit hit;
        if(Physics.Raycast(c.center,c.orientation,out hit,100,plateauLayer))
        {
            transform.position = hit.point + hit.normal * 0.01f; //decal a bit to avoid mesh overlap
            transform.LookAt(hit.point + hit.normal);
        }
    }

    public void setColor(Color c)
    {
        GetComponent<Renderer>().material.color = c;
    }
}
