using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityOSC;

[RequireComponent(typeof(Collider))]
public class MotionMapZone : MonoBehaviour {

    public string id;
    public Renderer[] objects;

    List<Material> materials;
    List<Color> initMaterialColors; //keep init color to revert

    [HideInInspector]
    public float overStartTime;
    [HideInInspector]
    public bool isOverInThisFrame; //to verify for not over event (without setOver on each frame)

    [HideInInspector]
    public bool over;
    [HideInInspector]
    public bool selected;
   

    public float selectionProgression;

	// Use this for initialization
	void Start () {
        if (id == "") id = gameObject.name;

        materials = new List<Material>();
        initMaterialColors = new List<Color>();
	    foreach(Renderer r in objects)
        {
            foreach(Material m in r.materials)
            {
                materials.Add(m);
                initMaterialColors.Add(m.color);
            }
        }
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    public void setOver(bool value)
    {
        if (over == value) return;

        over = value;

        if (!selected)
        {
            if (value)
            {
                overStartTime = Time.time;
                setAllMaterialsColors(Color.yellow);
            }
            else
            {
                resetAllMaterialsColors();
            }
        }

        OSCMessage m = new OSCMessage(value ? "/zone/"+id+"/over" : "/zone/"+id+"/out");
        OSCMaster.sendMessage(m); 

    }

    public void setSelectionProgression(float value)
    {
        if (!selected)
        { 
            selectionProgression = Mathf.Clamp01(value);
            OSCMessage m = new OSCMessage("zone/" + id + "/selectionProgress", selectionProgression);
            OSCMaster.sendMessage(m);
        }
    }

    public void setSelected(bool value)
    {
        if (selected == value) return;

        selected = value;
        if (value)
        {
            setAllMaterialsColors(Color.green);
        }else
        {
            resetAllMaterialsColors();
        }

        OSCMessage m = new OSCMessage(value?"/zone/"+id+"/selected":"/zone/"+id+"/deselected");
        OSCMaster.sendMessage(m);
    }

    void OnDrawGizmos()
    {
        Color c= Color.yellow;
        c.a = .3f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale);
        Gizmos.color = new Color(1f, 1f, 0,.6f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }


    void setAllMaterialsColors(Color targetColor, float time = .5f)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            m.DOColor(targetColor, time);
        }
    }

    void resetAllMaterialsColors(float time = .5f)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            Color c = initMaterialColors[i];
            m.DOColor(c, time);
        }
    }
}
