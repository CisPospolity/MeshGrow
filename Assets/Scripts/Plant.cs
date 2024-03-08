using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plant : MonoBehaviour
{
    private Controls controls;

    [SerializeField]
    private GameObject plantSegmentPrefab;
    [SerializeField]
    private GameObject topGameObject;
    [SerializeField]
    private GameObject leaf;
    [SerializeField]
    private Transform cam;
    [SerializeField]
    private AudioSource audio;
    private bool isGrowing;
    private bool isTurning;
    [SerializeField]
    private Vector3 growthDirection = Vector3.up;
    [SerializeField]
    private Vector3 targetGrowthDirection = Vector3.up;

    [SerializeField]
    private Vector3 point = Vector3.up * 0.5f;

    [SerializeField]
    private float distanceToNewSegment = 0.5f;
    private Transform lastSegment;

    private Queue<Transform> topSegments;
    [SerializeField]
    private Transform segmentsParent;
    [SerializeField]
    private int segmentsToMove = 4;
    [SerializeField]
    private float growingSpeed = 5f;
    [SerializeField]
    private float segmentMoveSpeed = 0.4f;
    [SerializeField]
    private float minSegmentSize = 0.7f;
    [SerializeField]
    private float maxSegmentSize = 1f;
    [SerializeField]
    private float segmentGrowthTime = 0.3f;
    [SerializeField]
    private float minLeafSize = 0f;
    [SerializeField]
    private float maxLeafSize = 1f;
    [SerializeField]
    private float leafGrowthTime = 2f;

    private void Awake()
    {
        controls = new Controls();
        controls.MainControls.Grow.performed += ctx => Grow();
        controls.MainControls.Grow.canceled += ctx => StopGrow();
        controls.MainControls.GrowthDirection.performed += ctx => StartTurning();
        controls.MainControls.GrowthDirection.canceled += ctx => StopTurning();

        lastSegment = transform;
        topSegments = new Queue<Transform>();
        point = topGameObject.transform.position;
    }

    private void Update()
    {
        topGameObject.transform.LookAt(point, topGameObject.transform.up);
        topGameObject.transform.position = point;
        UpdateGrowthDirection();

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

    private void Grow()
    {
        isGrowing = true;
        audio.Play();
        StartCoroutine(GrowPlant());
    }

    private void StopGrow()
    {
        isGrowing = false;
        audio.Stop();
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
            if (!isGrowing) break;

            if (isTurning)
            {
                growthDirection = Vector3.Slerp(growthDirection, targetGrowthDirection, Time.deltaTime);
            }

            if(Vector3.Distance(point, lastSegment.position) >= distanceToNewSegment)
            {
                GameObject go = Instantiate(plantSegmentPrefab, point + growthDirection * 0.1f, Quaternion.identity, segmentsParent);
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, point - go.transform.position);
                lastSegment = go.transform;
                if(topSegments.Count >= segmentsToMove)
                {
                    topSegments.Dequeue();
                }
                topSegments.Enqueue(lastSegment);
                StartCoroutine(ExpandSegment(lastSegment));
            }

            var segments = topSegments.ToArray();
            point += growthDirection * growingSpeed * Time.deltaTime;
            for(int i=0; i<segments.Length; i++)
            {
                segments[i].position += growthDirection / (segments.Length - i) * segmentMoveSpeed * Time.deltaTime;
            }
            yield return null;
        }
    }

    private IEnumerator ExpandSegment(Transform segment)
    {
        float elapsedTime = 0f;
        while(elapsedTime < segmentGrowthTime)
        {
            if(isGrowing)
            {
                elapsedTime += Time.deltaTime;
            }

            segment.localScale = Vector3.one * Mathf.SmoothStep(minSegmentSize,maxSegmentSize, elapsedTime/segmentGrowthTime);
            yield return null;
        }
        if(segment.GetComponentInChildren<MeshFilter>() != null)
        {
            var meshFilter = segment.GetComponentInChildren<MeshFilter>();
            var randomNumber = Random.Range(0, meshFilter.mesh.vertices.Length);
            Vector3 vertice = meshFilter.mesh.vertices[randomNumber];
            Vector3 normal = meshFilter.mesh.normals[randomNumber];

            Vector3 vertexPosition = segment.transform.TransformPoint(vertice);
            Vector3 vertexNormal = segment.transform.TransformDirection(normal);

            Quaternion rotation = Quaternion.FromToRotation(segment.up, vertexNormal);
            var leafGo = Instantiate(leaf, vertexPosition, rotation);
            leafGo.transform.localScale = Vector3.one * minLeafSize;

            StartCoroutine(ExpandLeaf(leafGo.transform));
            //leaf.transform.up = vertexNormal;

        }
    }

    private IEnumerator ExpandLeaf(Transform leaf)
    {
        float elapsedTime = 0f;
        while (elapsedTime < leafGrowthTime)
        {
            if (isGrowing)
            {
                elapsedTime += Time.deltaTime;
            }

            leaf.localScale = Vector3.one * Mathf.SmoothStep(minLeafSize, maxLeafSize, elapsedTime / leafGrowthTime);
            yield return null;
        }
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
