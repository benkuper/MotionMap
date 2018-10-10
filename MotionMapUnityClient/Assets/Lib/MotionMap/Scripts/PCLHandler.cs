using UnityEngine;
using System.Collections;
using Windows.Kinect;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using CielaSpike;

public class Cluster
{
    public int[] indices;
    public int id;
    public int numPoints;
    public Vector3 center;
    public Vector3 orientation;

    public Cluster(int[] indices, int clusterID, Vector3 center)
    {
        this.id = clusterID;
        this.indices = indices;
        numPoints = indices.Length;
        this.center = center;
    }
}

public class PCLHandler : MonoBehaviour
{

    bool isInit = false;

    public delegate void ClusterEvent(Cluster c);
    public event ClusterEvent clusterAdded;
    public event ClusterEvent clusterUpdated;
    public event ClusterEvent clusterRemoved;


    KinectSensor sensor;
    DepthFrameReader depthReader;
    ColorFrameReader colorReader;
    CoordinateMapper mapper;

    ushort[] frameData;
    CameraSpacePoint[] cameraPoints;
    DepthSpacePoint[] depthPoints;
    Vector3[] worldPoints;

    byte[] depthTexData;
    Texture2D depthTex;

    byte[] colorData;
    Texture2D colorTex;
    CameraSpacePoint[] colorPoints;


    public bool kinectIsInit { get; private set; }
    public int depthWidth { get; private set; }
    public int depthHeight { get; private set; }
    int colorWidth, colorHeight;

    public int numGoodPoints;


    [Header("PointCloud Configuration")]
    public bool useCollider;
    public BoxCollider pclCollider;
    [Range(1, 20)]
    public int steps = 4;
    [Range(1, 20)]
    public int debugSteps = 4;

    [Header("Visualization")]
    public Camera mainCamera;
    public bool showPCL;
    [Range(0, .1f)]
    public float pointSize;
    [Range(0, .1f)]
    public float debugPointSize;

    public Material pclMat;

    [Header("Color Feedback")]
    public UnityEngine.UI.RawImage feedbackImage;

    //KD Tree
    [Header("KdTree")]
    public bool processClusters;
    [Range(0, .1f)]
    public float clusterMaxDist = 0.01f;

    bool clusterReady;
    Vector3[] clusterPoints;

    [Range(0, 1000)]
    public int minClusterSize;
    public int numClusters;

    KDTree tree;
    public List<Cluster> clusters;
    List<int> clusterQueue;
    bool[] processedIndices;
    Task clusterTask;

    [Header("Cluster Orientation")]
    public bool processClusterOrientation;
    [Range(0,1)]
    public float numPointsForOrientation = .2f;
    public Transform pointingTarget;
    public Vector3 pointingPos;

    [Header("Kinect Calibration")]
    public bool colorMode;
    public Transform[] targets;
    public Vector3[] calib3DPoints;
    public int currentCalibPoint = -1;
    Color[] calibPointColors = new Color[3] { Color.yellow, Color.red, Color.blue };

    [Header("Debug")]
    public bool debugMode;
    public bool drawInEditor;
    public bool updatePCL;
    public bool updateTree;
    public float orientationLineFactor = 1;

    //tmp colorMap
    public Vector3 colorPos;
    Color[] clusterColors;
    
    void Awake()
    {
        tree = new KDTree(); //avoid null, will be replace after
        clusters = new List<Cluster>();

        clusterColors = new Color[20];
        for (int i = 0; i < clusterColors.Length; i++)
        {
            clusterColors[i] = Color.HSVToRGB(i * .31f, 1, 1);
        }

        clusterReady = true;

        calib3DPoints = new Vector3[targets.Length];
        for(int i=0;i<calib3DPoints.Length;i++)
        {
            calib3DPoints[i] = Vector3.zero;
        }

        Invoke("init", 1);
    }

    private void init()
    {
        load();
        initKinect();
        isInit = true;
    }

    void Update()
    {
        if (!isInit) return;

        updateKinect();

        if (!processClusters || clusterReady)
        {
            updateWorldPoints();
            updateKDTree();
        }else
        {
            //Debug.Log("skip update, wait for task to finish");
        }
        

        if (Input.GetMouseButtonDown(1))
        {
            colorMode = !colorMode;
            feedbackImage.texture = colorMode ? colorTex : depthTex;
        }
        
    }

    void OnEnable()
    {
        Camera.onPostRender += postRender;
    }

    void OnDisable()
    {
        save();

        Camera.onPostRender -= postRender;
        if(depthReader != null) depthReader.Dispose();
        sensor.Close();
        if(clusterTask != null) clusterTask.Cancel();
    }

    void OnApplicationQuit()
    {
        if(depthReader != null)
        {
            depthReader.Dispose();

        }
        sensor.Close();
        if(clusterTask != null) clusterTask.Cancel();
    }

    ////////  KINECT

    void initKinect()
    {
        sensor = KinectSensor.GetDefault();
        if (sensor == null)
        {
            Debug.LogWarning("Sensor is null");
            kinectIsInit = false;
            return;
        }

        sensor.Open();

        if (!sensor.IsOpen)
        {
            Debug.LogWarning("Sensor is not open");
            kinectIsInit = false;
            return;
        }

        //Depth
        depthReader = sensor.DepthFrameSource.OpenReader();

        mapper = sensor.CoordinateMapper;

        depthWidth = depthReader.DepthFrameSource.FrameDescription.Width;
        depthHeight = depthReader.DepthFrameSource.FrameDescription.Height;

        frameData = new ushort[depthWidth * depthHeight];
        cameraPoints = new CameraSpacePoint[depthWidth * depthHeight];
        worldPoints = new Vector3[depthWidth * depthHeight];

        clusterPoints = new Vector3[worldPoints.Length];

        depthTexData = new byte[depthWidth * depthHeight * 3]; //RGB = 3 bytes per pixel
        depthTex = new Texture2D(depthWidth, depthHeight, TextureFormat.RGB24, false);


        //Color
        colorReader = sensor.ColorFrameSource.OpenReader();

        colorWidth = colorReader.ColorFrameSource.FrameDescription.Width;
        colorHeight = colorReader.ColorFrameSource.FrameDescription.Height;

        colorData = new byte[colorWidth * colorHeight * 4]; //RGBA = 4 bytes per pixel
        colorTex = new Texture2D(colorWidth, colorHeight, TextureFormat.RGBA32, false);
        colorPoints = new CameraSpacePoint[colorWidth * colorHeight];

        feedbackImage.texture = colorMode ? colorTex : depthTex;
        Debug.Log("Kinect is initialized.");

        kinectIsInit = true;
    }

    void updateKinect()
    {

        if (!kinectIsInit || !sensor.IsOpen || depthReader == null)
        {
            Debug.Log("Kinect is not init or open");
            initKinect();
            return;
        }

        DepthFrame depthFrame = depthReader.AcquireLatestFrame();

        if (depthFrame != null)
        {
            depthFrame.CopyFrameDataToArray(frameData);
            mapper.MapDepthFrameToCameraSpace(frameData, cameraPoints);
            depthFrame.Dispose();

            for (int i = 0; i < frameData.Length; ++i)
            {
                depthTexData[3 * i + 0] = (byte)(frameData[i] * 1f / 20);
                depthTexData[3 * i + 1] = (byte)(frameData[i] * 1f / 20);
                depthTexData[3 * i + 2] = (byte)(frameData[i] * 1f / 20);
            }

            depthTex.LoadRawTextureData(depthTexData);
            depthTex.Apply();
        }

        
        ColorFrame colorFrame = colorReader.AcquireLatestFrame();
        if(colorFrame != null)
        {
            colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Rgba);
            colorFrame.Dispose();

            colorTex.LoadRawTextureData(colorData);
            colorTex.Apply();

            mapper.MapColorFrameToCameraSpace(frameData, colorPoints);
        }

    }

    void updateWorldPoints()
    {
        if (!kinectIsInit) return;
        if (!updatePCL) return;

        int _steps = debugMode ? debugSteps : steps;

        numGoodPoints = 0;
        for (int ty = 0; ty < depthHeight; ty += _steps)
        {
            for (int tx = 0; tx < depthWidth; tx += _steps)
            {
                int index = ty * depthWidth + tx;
                CameraSpacePoint p = cameraPoints[index];

                if (float.IsInfinity(p.X) || float.IsNaN(p.X)) continue;

                Vector3 worldPoint = transform.TransformPoint(new Vector3(-p.X, p.Y, p.Z)); //Mirror

                if (useCollider && !pclCollider.bounds.Contains(worldPoint)) continue;

                worldPoints[numGoodPoints] = worldPoint;
                numGoodPoints++;
            }
        }


        
    }

    void updateKDTree()
    {
        if (!updateTree) return;
        tree = KDTree.MakeFromPoints(numGoodPoints, worldPoints);

        //Segmentationit
        processedIndices = new bool[numGoodPoints];
        clusterQueue = new List<int>();

        
        if (processClusters )
        {
            pointingPos = pointingTarget.position;
            this.StartCoroutineAsync(processClustersAsync(),out clusterTask);
        }
        
    }

    IEnumerator processClustersAsync()
    {
        processClustersInternal();
        yield return true;
    }

    void processClustersInternal()
    {
        clusterReady = false;
        List<Cluster> newClusters = new List<Cluster>();

        
        int numProcessedPoints = 0;
        while (numProcessedPoints < numGoodPoints)
        {
            clusterQueue.Clear();

            Vector3 clusterCenter = new Vector3();

            //Check for first not processed points
            for (int i = 0; i < numGoodPoints; i++)
            {
                if (!processedIndices[i])
                {
                    clusterQueue.Add(i);
                    break;
                }
            }

            //Loop through clusterQueue (which is growing inside the loop) and process points
            for (int i = 0; i < clusterQueue.Count; i++)
            {
                processedIndices[clusterQueue[i]] = true;
                numProcessedPoints++;
                clusterCenter += worldPoints[clusterQueue[i]];

                List<int> indices = tree.FindNearestsRadius(worldPoints[clusterQueue[i]], clusterMaxDist);
                for (int j = 0; j < indices.Count; j++)
                {
                    int ind = indices[j];
                    if (ind >= numGoodPoints) continue;
                    if (!processedIndices[ind])
                    {
                        processedIndices[ind] = true;
                        clusterQueue.Add(ind);
                    }
                }
            }

            clusterCenter /= clusterQueue.Count;
            if (clusterQueue.Count >= minClusterSize)
            {
                Cluster c = new Cluster(clusterQueue.ToArray(), -1, clusterCenter);
                newClusters.Add(c);
            }
        }

        //Track clusters (correspondance)
        for (int i = 0; i < newClusters.Count; i++)
        {
            int nearestIndice = -1;
            float minDist = 1000;

            for (int j = 0; j < clusters.Count; j++)
            {
                float dist = Vector3.Distance(clusters[j].center, newClusters[i].center);
                if (dist < minDist)
                {
                    nearestIndice = j;
                    minDist = dist;
                }
            }
            
            if (nearestIndice >= 0)
            {
                newClusters[i].id = clusters[nearestIndice].id;
                clusters.RemoveAt(nearestIndice);
            }
            
        }


        //All remaining clusters in the current clusters list are removed ones, notify
        foreach(Cluster c in clusters)
        {
            if (clusterRemoved != null) clusterRemoved(c);
        }

        //For all clusters that have not got ids (which mean they are new), assign new ids and notify
        for (int i = 0; i < newClusters.Count ; i++)
        {
            if (newClusters[i].id != -1) continue;
            newClusters[i].id = getNextClusterId(newClusters);
            if(clusterAdded != null) clusterAdded(newClusters[i]);
        }
        


        //Check for removed clusters
        /*
         for (int i = 0; i < clusters.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < newClusters.Count; j++)
            {
                if (clusters[i].id == newClusters[j].id)
                {
                    //found
                    found = true;
                    break;
                }
            }
            if(!found)
            {
                if (clusterAdded != null) clusterRemoved(clusters[i]);
            }
        }
        */

        //Check for added cluster
        /*
        for (int i = 0; i < newClusters.Count; i++)
        {
            bool found = false;
            for (int j = 0; j < clusters.Count; j++)
            {
                if (newClusters[i].id == clusters[j].id)
                {
                    //found
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                if (clusterAdded != null) clusterAdded(newClusters[i]);
            }
        }
        */

        clusters = newClusters;
        numClusters = clusters.Count;

        Array.Copy(worldPoints, clusterPoints, numGoodPoints);

        if (processClusterOrientation) computeClusterOrientation();
        
        for(int i = 0;i < numClusters;i++)
        {
            if (clusters[i] == null) continue;
            clusterUpdated(clusters[i]);
        }

        clusterReady = true;

        
    }

    void computeClusterOrientation()
    {
        foreach(Cluster c in clusters)
        {

            int numRPoints = (int)(numPointsForOrientation * c.numPoints);
            Vector3[] points = new Vector3[numRPoints];
            Vector3 mainOrientation = new Vector3();
            Vector3 absOrientation = new Vector3();
            //System.Random rand = new System.Random();
            for (int i=0;i< c.numPoints; i++)
            {
                //int index = rand.Next(0, c.numPoints);
                points[i] = clusterPoints[c.indices[i]];
            }

            int numDone = 0;
            for(int i=0;i<numRPoints;i++)
            {
                if (points[i] == Vector3.zero) continue;// Debug.Log("zero")

                for (int j=i+1;j<numRPoints;j++)
                {
                    if (points[j] == Vector3.zero) continue;// Debug.Log("zero")

                    if (points[i] == points[j]) continue;
                    mainOrientation += points[j] - points[i];
                    absOrientation += new Vector3(Math.Abs(mainOrientation.x), Math.Abs(mainOrientation.y), Math.Abs(mainOrientation.z));
                    numDone++;
                }
            }

            //absOrientation /= numDone;
            //mainOrientation /= numDone;
            //mainOrientation.Normalize();
            absOrientation.Normalize();
            absOrientation *= .001f;
            
           // Debug.Log(clusters.IndexOf(c) + " : " + numDone+" > "+mainOrientation+"/"+absOrientation);
            
            if (mainOrientation.x >= 0) absOrientation.x = -absOrientation.x;
            if (mainOrientation.z >= 0) absOrientation.z = -absOrientation.z;
            
            if(((c.center + absOrientation)-pointingPos).magnitude > (c.center- pointingPos).magnitude)
            {
                absOrientation = -absOrientation;
                //Debug.Log("Inverse !");// absOrientation -= absOrientation;
            }

            //Debug.Log(mainOrientation);
            c.orientation = absOrientation.normalized;
        }
    }

    int getNextClusterId(List<Cluster> clusterList)
    {
        int result = 0;
        while (true)
        {
            bool found = false;
            for (int i = 0; i < clusterList.Count; i++)
            {
                if (clusterList[i].id == result)
                {
                    found = true;
                    break;
                }
            }
            if (!found) break;
            result++;
        }
        return result;
    }


    void postRender(Camera cam)
    {
        if (!showPCL) return;

        if (!kinectIsInit) return;

        bool camCheck = false;
        if (Camera.current == mainCamera) camCheck = true;
        else if (Camera.current.cameraType == CameraType.SceneView && drawInEditor) camCheck = true;
        if (!camCheck) return;


        GL.PushMatrix();
        pclMat.SetPass(0);
        GL.Begin(GL.LINES);

        if (!processClusters)
        {
            for (int i = 0; i < numGoodPoints; i++)
            {
                Vector3 p = worldPoints[i];
                drawCross(p);
            }

            //GL.Color(Color.red);
           // drawCross(colorPos,4);
        }
        else
        {
            for (int i = 0; i < clusters.Count; i++)
            {
                Cluster c = clusters[i];
                GL.Color(Color.white);
                drawCross(c.center, 4);

                GL.TexCoord(Vector3.one*.5f);
                GL.Vertex(c.center);
                GL.Vertex(c.center + c.orientation*orientationLineFactor);


                Color col = clusterColors[Mathf.Max(c.id,0) % clusterColors.Length];
                GL.Color(col);

                for (int j = 0; j < c.numPoints; j++)
                {
                    int index = c.indices[j];
                    Vector3 p = clusterPoints[index];
                    drawCross(p);
                }
            }
            
        }

        GL.End();
        GL.PopMatrix();

    }

    void drawCross(Vector3 p, float sizeFactor = 1)
    {
        float targetSize = (debugMode?debugPointSize:pointSize) * sizeFactor;
        GL.TexCoord(Vector3.zero);
        GL.Vertex(p - Vector3.forward * targetSize);
        GL.TexCoord(Vector3.right);
        GL.Vertex(p + Vector3.forward * targetSize);
        GL.TexCoord(Vector3.zero);
        GL.Vertex(p - Vector3.right * targetSize);
        GL.TexCoord(Vector3.right);
        GL.Vertex(p + Vector3.right * targetSize);
        GL.TexCoord(Vector3.zero);
        GL.Vertex(p - Vector3.up * targetSize);
        GL.TexCoord(Vector3.right);
        GL.Vertex(p + Vector3.up * targetSize);
    }


    void updateKinectCalib()
    {
        for(int i=0;i<targets.Length;i++)
        {
            calib3DPoints[i] = getSpacePointForTarget(targets[i]);
        }

        if (float.IsInfinity(calib3DPoints[0].x)) return;


        transform.parent.rotation = Quaternion.identity;
        transform.localPosition = -calib3DPoints[0];

        Vector3 ba = calib3DPoints[1] - calib3DPoints[0];
        Vector3 ca =  calib3DPoints[2] - calib3DPoints[1];
        Vector3 normXZ = Vector3.Cross(ca, ba);
        normXZ.Normalize();

        Vector3 calibAxisNorm = Vector3.Cross(normXZ, Vector3.up);
        float angle = Vector3.Angle(normXZ, Vector3.up);
        transform.parent.Rotate(calibAxisNorm, angle);

        Vector3 xRel = transform.TransformPoint(calib3DPoints[1]);
        xRel.Normalize();
        float yAngle = Vector3.Angle(xRel, Vector3.right);
        Vector3 euler = transform.parent.localRotation.eulerAngles;
        transform.parent.localRotation = Quaternion.Euler(euler.x, euler.y + yAngle, euler.z);

    }

    //UI
    public void uiTargetDrag(Transform t)
    {
        t.position = Input.mousePosition;
        updateKinectCalib();
    }

    Vector3 getSpacePointForTarget(Transform target)
    {
        Vector2 localCursor = target.localPosition;
        RectTransform r = target.parent.GetComponent<RectTransform>();
        localCursor = Vector2.Scale(localCursor, new Vector2(1f / r.rect.width, 1f / r.rect.height)) + Vector2.one * .5f;

        if (localCursor.x < 0 || localCursor.y < 0 || localCursor.x >= 1 || localCursor.y >= 1) return Vector3.zero;

        int tWidth = colorMode ? colorWidth : depthWidth;
        int tHeight = colorMode ? colorHeight : depthHeight;
        CameraSpacePoint[] tPoints = colorMode ? colorPoints : cameraPoints;

        int tx = (int)(localCursor.x * tWidth);
        int ty = (int)(localCursor.y * tHeight);
        int index = ty * tWidth + tx;
        CameraSpacePoint csp = tPoints[index];
        return new Vector3(-csp.X, csp.Y, csp.Z);
    }

    void OnDrawGizmos()
    {
        for (int i = 0; i < calib3DPoints.Length; i++)
        {
            Gizmos.color = calibPointColors[i];
            Gizmos.DrawSphere(transform.TransformPoint(calib3DPoints[i]), .02f);
        }
    }

    void save()
    {
        SaveData data = new SaveData();
        for (int i = 0; i < targets.Length; i++) data["target" + i] = targets[i].localPosition;
        data.Save(Application.dataPath + "/pcl_calib.uml");
    }

    public void load()
    {
        SaveData data = SaveData.Load(Application.dataPath + "/pcl_calib.uml");
        if (data == null) return;

        for (int i = 0; i < targets.Length; i++) targets[i].localPosition = data.GetValue<Vector3>("target" + i);

        Invoke("updateKinectCalib", 3);
    }

    //UI Helpers
    public void setDebugMode(bool value)
    {
        debugMode = value;
        processClusters = !value;
        useCollider = !value;
        updateWorldPoints();
        updatePCL = !value;
    }
}