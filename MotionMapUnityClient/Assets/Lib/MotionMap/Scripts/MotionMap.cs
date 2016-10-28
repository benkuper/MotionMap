using UnityEngine;
using System.Collections.Generic;
using System;

public class MotionMap : MonoBehaviour {

    public PCLHandler handler;

    [Header("Cursor")]
    public GameObject cursorPrefab;
    public bool useDifferentColorsForIds;

    List<MotionMapCursor> cursors;
    
    [Header("Zone Selection")]
    public float selectionTime = 2;

    [Header("Demo mode")]
    public float demoModeTime = 30;
    bool demoMode;

    int sceneLayer;

    MotionMapZone[] zones;
    MotionMapZone selectedZone;

	void Start () {
        sceneLayer = LayerMask.NameToLayer("scene");

        zones = FindObjectsOfType<MotionMapZone>();
        cursors = new List<MotionMapCursor>();

        handler.clusterAddedHandler += clusterAdded;
        handler.clusterRemovedHandler += clusterRemoved;
	}

    
    void Update () {
        foreach (MotionMapZone z in zones) z.isOverInThisFrame = false;

        for (int i=0;i<cursors.Count;i++)
        {
            RaycastHit hit;
            Cluster c = cursors[i].c;
            if (Physics.Raycast(c.center, c.orientation, out hit, 100, sceneLayer))
            {
                MotionMapZone z = hit.collider.GetComponent<MotionMapZone>();
                if(z != null)
                {
                    z.isOverInThisFrame = true;
                }
            }
        }

        foreach (MotionMapZone z in zones)
        {
            z.setOver(z.isOverInThisFrame);
            if(z.over)
            {
                float curSelectTime = (Time.time - z.overStartTime) / selectionTime;
                z.setSelectionProgression(curSelectTime);
                if(curSelectTime >= 1)
                {
                    setSelectedZone(z);
                }
            }                
        }

    }

    void OnDisable()
    {
        handler.clusterAddedHandler -= clusterAdded;
        handler.clusterRemovedHandler -= clusterRemoved;
    }

    private void clusterRemoved(Cluster c)
    {
        cursors.Remove(getCursorForCluster(c));

        if(handler.clusters.Count == 0)
        {
            Invoke("startDemo", demoModeTime);
        }
    }

    private void clusterAdded(Cluster c)
    {
        MotionMapCursor cursor = Instantiate(cursorPrefab).GetComponent<MotionMapCursor>();
        cursor.transform.SetParent(transform.FindChild("Cursors"));
        cursors.Add(cursor);
        cursor.setColor(UnityEngine.Random.ColorHSV(.2f, .8f, 1, 1, 1, 1));

        CancelInvoke("startDemo");
        if(demoMode) stopDemo();
    }


    public void setSelectedZone(MotionMapZone zone)
    {
        if (zone == selectedZone) return;
        if(selectedZone != null)
        {
            selectedZone.setSelected(false);
        }

        selectedZone = zone;

        if(selectedZone != null)
        {
            selectedZone.setSelected(true);
        }


        OSCMaster.sendMessage("targetChanged", new object[] { selectedZone.id });
    }



    public void startDemo()
    {
        demoMode = true;
    }

    public void stopDemo()
    {
        demoMode = false;
    }

    public MotionMapCursor getCursorForCluster(Cluster c)
    {
        foreach(MotionMapCursor mmc in cursors)
        {
            if (mmc.c == c) return mmc;
        }

        return null;
    }

}
