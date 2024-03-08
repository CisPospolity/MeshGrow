using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshGrower : MonoBehaviour
{
    private Controls controls;

    [Header("Prefabs")]
    [SerializeField]
    private Transform pointGameobject;
    [SerializeField]
    private Transform cam;
    [SerializeField]
    private AudioSource stretchSound;

    [Header("Growth")]
    [SerializeField]
    private Vector3 point = Vector3.zero;
    [SerializeField]
    private Vector3 lastPoint;

    [SerializeField]
    private Vector3 growthDirection = Vector3.up;
    [SerializeField]
    private Vector3 targetGrowthDirection = Vector3.up;
    [SerializeField]
    private float initialGrowthHeight = 0.1f;
    [SerializeField]
    private float initialGrowthRadius = 0.1f;
    [SerializeField]
    private float growthRate = 50f;
    [SerializeField]
    private float turningRate = 1f;

    [Header("Segments")]
    [SerializeField]
    private float segmentHeight = 1f;
    [SerializeField]
    private float minSegmentHeight = 0.1f;
    [SerializeField]
    private float timeToExpand = 2f;

    public int subdivisions = 24;
    public float height = 1f;
    public float radius = 1f;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;

    private bool isGrowing = false;
    private float currentSegmentGrowth = 0f;

    private List<int> topCircleVertices;
    private List<int> secondToLastCircleVertices;


    private bool isTurning = false;

    
    

    private void Awake()
    {
        controls = new Controls();
        controls.MainControls.Grow.performed += ctx => Grow();
        controls.MainControls.Grow.canceled += ctx => StopGrow();
        controls.MainControls.GrowthDirection.performed += ctx => StartTurning();
        controls.MainControls.GrowthDirection.canceled += ctx => StopTurning();
        controls.MainControls.LockCursor.performed += ctx => ChangeCursorLock();
        controls.MainControls.Reset.performed += ctx => ChangeLevel(SceneManager.GetActiveScene().name);
    }


    private void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        topCircleVertices = new List<int>();
        secondToLastCircleVertices = new List<int>();


        vertices = new Vector3[(subdivisions + 1) * 2];
        triangles = new int[subdivisions * 6];

        //GeneratePlantBase();
        AddNewSegment(initialGrowthRadius);
        UpdateMesh();
    }

    private void Update()
    {
        pointGameobject.position = point;
        UpdateGrowthDirection();
            UpdateMesh();
    }

    private void ChangeCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private Vector2 MovementDirection()
    {
        return controls.MainControls.GrowthDirection.ReadValue<Vector2>().normalized;
    }

    private void UpdateGrowthDirection()
    {
        Vector2 dir2D = MovementDirection();

        if (dir2D != Vector2.zero)
        {
            targetGrowthDirection = cam.forward * dir2D.y + cam.right * dir2D.x;
        }
    }

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    private void Grow()
    {
        isGrowing = true;
        StartCoroutine(GrowPlant());
        stretchSound.Play();

    }

    private void StopGrow()
    {
        isGrowing = false;
        stretchSound.Stop();
    }


    private void AddNewSegment(float startRadius)
    {
        point += growthDirection*initialGrowthHeight;

        var newVertices = new List<Vector3>(vertices);
        var newTriangles = new List<int>(triangles);

        secondToLastCircleVertices.Clear();
        secondToLastCircleVertices.AddRange(topCircleVertices);

        topCircleVertices.Clear();


        int previousCircleStartIndex = newVertices.Count - (subdivisions + 1);

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, growthDirection.normalized);

        // Add new top circle vertices
        for (int i = 0; i <= subdivisions; i++)
        {
            float angle = i * 2 * Mathf.PI / subdivisions;
            float x = Mathf.Cos(angle) * startRadius;
            float z = Mathf.Sin(angle) * startRadius;

            Vector3 vertexPosition = new Vector3(x, 0, z);
            vertexPosition = rotation * vertexPosition + point;

            newVertices.Add(vertexPosition);
            topCircleVertices.Add(newVertices.Count - 1);

            if (i < subdivisions)
            {
                int current = newVertices.Count - 1;
                int next = current + 1;
                int previous = previousCircleStartIndex + i;
                int previousNext = previous + 1;

                newTriangles.Add(previous);
                newTriangles.Add(current);
                newTriangles.Add(next);

                newTriangles.Add(previous);
                newTriangles.Add(next);
                newTriangles.Add(previousNext);
            }
        }

        vertices = newVertices.ToArray();
        triangles = newTriangles.ToArray();

        StartCoroutine(ExpandCircle());
    }

    private void GrowTop(float newHeight)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, growthDirection.normalized);

        foreach (int i in topCircleVertices)
        {
            float angle = 2 * Mathf.PI * (i % (subdivisions + 1)) / subdivisions;
            Vector3 circlePosition = new Vector3(Mathf.Cos(angle) * initialGrowthRadius, 0, Mathf.Sin(angle) * initialGrowthRadius);
            circlePosition = rotation * circlePosition; // Rotate the position
            vertices[i] = circlePosition + point;
        }
        point += (newHeight - height) * growthRate * growthDirection;
    }

    void ResizeSecondToLastCircle(float newRadius, List<int> verticesToExpand, Vector3 lastPos)
    {
        Vector3 circleCenter = Vector3.zero;
        foreach (int index in verticesToExpand)
        {
            circleCenter += vertices[index];
        }
        circleCenter /= verticesToExpand.Count;

        // Calculate rotation to align with the growth direction
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, growthDirection.normalized);

        for (int i = 0; i < verticesToExpand.Count; i++)
        {
            int vertexIndex = verticesToExpand[i];
            Vector3 vertex = vertices[vertexIndex];
            // Calculate direction from center to vertex
            Vector3 direction = (vertex - circleCenter).normalized;

            // Remove previous rotation
            direction = Quaternion.Inverse(rotation) * direction;

            // Resize based on new radius and re-apply rotation
            direction *= newRadius;
            vertices[vertexIndex] = lastPos + rotation * direction;
        }
    }

    private bool IsTurning()
    {
        return isTurning;
    }

    private void StartTurning()
    {
        isTurning = true;
    }

    private void StopTurning()
    {
        isTurning = false;

    }

    private IEnumerator GrowPlant()
    {

        while (isGrowing)
        {
            if (!isGrowing)
            {
                break;
            }

            if (isTurning)
            {
                growthDirection = Vector3.Slerp(growthDirection, targetGrowthDirection, turningRate * Time.deltaTime);
            }

            float targetHeight = height + (IsTurning() ? minSegmentHeight : segmentHeight); // Shorter segments if turning

            if (currentSegmentGrowth == 0)
            {
                AddNewSegment(initialGrowthRadius);
            }
            while (height + currentSegmentGrowth < targetHeight)
            {
                if(!isGrowing)
                {
                    break;
                }
                currentSegmentGrowth = Vector3.Distance(lastPoint, point);
                float currentHeight = height + currentSegmentGrowth;
                GrowTop(currentHeight);
                yield return null;
            }
            if (isGrowing)
            {
                // Reset for the next segment
                lastPoint = point;
                height = targetHeight;
                GrowTop(height);

                currentSegmentGrowth = 0;
            }
        }
    }

    IEnumerator ExpandCircle()
    {
        List<int> verticesToExpand = new List<int>(secondToLastCircleVertices);
        Vector3 lastPos = lastPoint;
        float elapsedTime = 0f;

        while(elapsedTime < timeToExpand)
        {
            if (isGrowing)
            {
                elapsedTime += Time.deltaTime;
            }

                float newRadius = Mathf.Lerp(initialGrowthRadius, radius, elapsedTime / timeToExpand);
                ResizeSecondToLastCircle(newRadius, verticesToExpand, lastPos);
            
            yield return null;
        }
        ResizeSecondToLastCircle(radius, verticesToExpand, lastPos);

    }

    public void ChangeLevel(string levelName)
    {
        SceneManager.LoadScene(levelName);
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }
}
