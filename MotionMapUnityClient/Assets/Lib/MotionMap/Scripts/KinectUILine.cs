using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class KinectUILine : MonoBehaviour {

    public RectTransform source;
    public RectTransform dest;

    RectTransform rt;
	// Use this for initialization
	void Start () {
        
	}
	
	// Update is called once per frame
	void Update () {
        if(rt == null) rt = GetComponent<RectTransform>();
        if (source == null ||  dest == null) return;

        rt.position = source.position;
        float angle = Mathf.Atan2(dest.position.y - source.position.y, dest.position.x - source.position.x);
        rt.rotation = Quaternion.AngleAxis(angle*180/Mathf.PI-90, Vector3.forward);
        rt.localScale = new Vector3(5, Vector2.Distance(source.position, dest.position));
	}
}
