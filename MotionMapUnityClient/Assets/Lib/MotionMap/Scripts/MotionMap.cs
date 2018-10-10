using UnityEngine;
using System.Collections.Generic;
using System;
using DG.Tweening;
using UnityOSC;

public class MotionMap : MonoBehaviour {

    public static MotionMap instance;

    public PCLHandler handler;

    [Header("Cursor")]
    public GameObject cursorPrefab;
    public bool useDifferentColorsForIds;
    public float cursorSmoothing;

    List<MotionMapCursor> cursors;
    List<Cluster> clustersToAdd;
    List<MotionMapCursor> clustersToRemove;

    [Header("Zone Selection")]
    public bool autoDeselectOnNewSelection;
    public float selectionTime = 2;
    public float progressionDecayTime = 1;

    [Header("Demo mode")]
    public float demoModeTime = 30;
    bool demoMode;

    int sceneLayer;
    int plateauLayer;

    MotionMapZone[] zones;
    MotionMapZone selectedZone;

    GameObject canvas;

	void Awake () {
        instance = this;

        sceneLayer = LayerMask.GetMask(new string[] { "scene" });
        plateauLayer = LayerMask.GetMask(new string[] { "plateau" });

        zones = FindObjectsOfType<MotionMapZone>();
        cursors = new List<MotionMapCursor>();

        clustersToAdd = new List<Cluster>();
        clustersToRemove = new List<MotionMapCursor>();

        Debug.Log("Add cluster listeners");
        handler.clusterAdded += clusterAdded;
        handler.clusterUpdated += clusterUpdated;
        handler.clusterRemoved += clusterRemoved;

        canvas = transform.Find("Canvas").gameObject;
        canvas.SetActive(false);
    }

    void Update () {
        foreach (MotionMapZone z in zones) z.isOverInThisFrame = false;

        foreach (MotionMapCursor mmc in clustersToRemove) removeCursorForCluster(mmc);
        foreach (Cluster c in clustersToAdd) addCursorForCluster(c);
        clustersToAdd.Clear();
        clustersToRemove.Clear();

        for (int i=0;i<cursors.Count;i++)
        {
            Vector3 targetPos = Vector3.zero;
            Vector3 targetRot = Vector3.up;

            RaycastHit hit;
            if (Physics.Raycast(cursors[i].clusterCenter, cursors[i].clusterOrientation, out hit, 100, sceneLayer ))
            {
                MotionMapZone z = hit.collider.GetComponent<MotionMapZone>();
                if(z != null)
                {
                    z.isOverInThisFrame = true;
                }
                targetPos = new Vector3(hit.transform.position.x, .01f, hit.transform.position.z);
                targetRot = Vector3.up;
            }
            else if (Physics.Raycast(cursors[i].clusterCenter, cursors[i].clusterOrientation, out hit, 100.0f, plateauLayer))
            {
                targetPos = hit.point + hit.normal * 0.01f;
                targetRot = hit.normal;
            }

            cursors[i].transform.DOMove(targetPos, cursorSmoothing); //decal a bit to avoid mesh overlap
            cursors[i].transform.DORotate(targetRot, cursorSmoothing);
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
            }else
            {
                z.setSelectionProgression(z.selectionProgression - Time.deltaTime / progressionDecayTime);
            }
        }

        if (Input.GetKeyDown(KeyCode.C)) canvas.SetActive(!canvas.activeInHierarchy);
    }

    void OnDisable()
    {
        handler.clusterAdded -= clusterAdded;
        handler.clusterUpdated -= clusterUpdated;
        handler.clusterRemoved -= clusterRemoved;
    }


    private void addCursorForCluster(Cluster c)
    {
        if(getCursorForClusterID(c) == null)
        {
            Debug.Log("Add cursor for cluster " + c.id);
            MotionMapCursor cursor = Instantiate(cursorPrefab).GetComponent<MotionMapCursor>();
            cursor.clusterID = c.id;
            cursor.transform.SetParent(transform.Find("Cursors"));
            cursors.Add(cursor);
            cursor.setColor(Color.HSVToRGB(cursor.clusterID * .31f, 1, 1));
        }
        else
        {
            Debug.Log("Cursor already exists for cluster " + c.id);
        }

        CancelInvoke("startDemo");
        if (demoMode) stopDemo();
    }

    private void removeCursorForCluster(MotionMapCursor mmc)
    {
        Debug.Log("Remove cursor ");
        cursors.Remove(mmc);
        Destroy(mmc.gameObject);
        
        if (handler.clusters.Count == 0)
        {
            Invoke("startDemo", demoModeTime);
        }
    }


    //Events
    private void clusterAdded(Cluster c)
    {
        Debug.Log("Cluster Added !");
        
        if (clustersToAdd.Contains(c)) return;
        clustersToAdd.Add(c);
    }

    private void clusterUpdated(Cluster c)
    {
       MotionMapCursor cursor = getCursorForClusterID(c);
       if(cursor == null)
        {
            Debug.Log("Cursor not found for updated cluster " + c.id);
            return;
        }

        cursor.update(c.center, c.orientation);
    }

    private void clusterRemoved(Cluster c)
    {

        Debug.Log("Cluster removed");
        MotionMapCursor mmc = getCursorForClusterID(c);
        if (clustersToAdd.Contains(c)) clustersToAdd.Remove(c);
        if(mmc == null)
        {
            Debug.Log("No cursor found for removed cluster " + c.id);
            return;
        }

        clustersToRemove.Add(mmc);
    }


    public void setSelectedZone(MotionMapZone zone)
    {
        if (zone == selectedZone) return;
        if(selectedZone != null)
        {
            if(autoDeselectOnNewSelection) selectedZone.setSelected(false);
        }

        selectedZone = zone;

        if(selectedZone != null)
        {
            selectedZone.setSelected(true);
        }


        OSCMessage m = new OSCMessage("/lastSelectedZone");
        m.Append(selectedZone.id);
        OSCMaster.sendMessage(m);
    }



    public void startDemo()
    {
        demoMode = true;
    }

    public void stopDemo()
    {
        demoMode = false;
    }
    

    public MotionMapCursor getCursorForClusterID(Cluster c)
    {
        foreach (MotionMapCursor mmc in cursors)
        {
            if (mmc.clusterID == c.id) return mmc;
        }

        return null;
    }
}
