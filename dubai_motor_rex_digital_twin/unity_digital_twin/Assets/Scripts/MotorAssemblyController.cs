using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotorAssemblyController : MonoBehaviour
{
    public MotorPart[] parts;
    public float animationSeconds = 0.65f;

    private readonly Dictionary<string, Transform> originalParents = new Dictionary<string, Transform>();
    private readonly Dictionary<string, Vector3> originalPositions = new Dictionary<string, Vector3>();
    private readonly Dictionary<string, Quaternion> originalRotations = new Dictionary<string, Quaternion>();

    private void Awake()
    {
        foreach (MotorPart part in parts)
        {
            if (part == null)
            {
                continue;
            }
            originalParents[part.componentId] = part.transform.parent;
            originalPositions[part.componentId] = part.transform.position;
            originalRotations[part.componentId] = part.transform.rotation;
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1.25f);
        StabilizePartsOnTable();
    }

    public void ResetAssembly()
    {
        foreach (MotorPart part in parts)
        {
            if (part == null)
            {
                continue;
            }
            RestoreOriginalParent(part);
            Rigidbody body = part.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            part.transform.position = originalPositions[part.componentId];
            part.transform.rotation = originalRotations[part.componentId];
            if (body != null)
            {
                body.isKinematic = false;
                body.WakeUp();
            }
        }
    }

    public Vector3 GetPartWorldPosition(string componentId)
    {
        MotorPart part = FindPart(componentId);
        return part != null ? part.transform.position : Vector3.zero;
    }

    public MotorPart GetPart(string componentId)
    {
        return FindPart(componentId);
    }

    public Vector3 GetPartGraspPosition(string componentId)
    {
        MotorPart part = FindPart(componentId);
        return part != null ? part.WorldBounds().center : Vector3.zero;
    }

    public Vector3 GetStationWorldPosition(string stationId)
    {
        return StationCenter(stationId);
    }

    public Vector3 GetStationPlacementPosition(string stationId, string componentId)
    {
        return NextStationPosition(stationId, componentId);
    }

    public void SynchronizePartCenter(string componentId, Vector3 worldCenter)
    {
        MotorPart part = FindPart(componentId);
        if (part == null || !originalParents.TryGetValue(componentId, out Transform originalParent))
        {
            return;
        }
        if (part.transform.parent != originalParent)
        {
            return;
        }

        Rigidbody body = part.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        for (int i = 0; i < 3; i++)
        {
            Physics.SyncTransforms();
            part.transform.position += worldCenter - part.WorldBounds().center;
        }
    }

    public void AttachPartToTool(string componentId, Transform tool)
    {
        MotorPart part = FindPart(componentId);
        if (part == null || tool == null)
        {
            return;
        }
        Rigidbody body = part.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        Vector3 localBoundsCenter = part.transform.InverseTransformPoint(part.WorldBounds().center);
        part.transform.SetParent(tool, true);
        part.transform.localRotation = Quaternion.identity;
        part.transform.position = tool.position - part.transform.TransformVector(localBoundsCenter);
        for (int i = 0; i < 3; i++)
        {
            Physics.SyncTransforms();
            part.transform.position += tool.position - part.WorldBounds().center;
        }
    }

    public void ReleasePartAtStation(string componentId, string stationId)
    {
        MotorPart part = FindPart(componentId);
        if (part == null)
        {
            return;
        }
        RestoreOriginalParent(part);
        StartCoroutine(MoveThenRelease(part, NextStationPosition(stationId, componentId)));
    }

    private IEnumerator MoveThenRelease(MotorPart part, Vector3 target)
    {
        Rigidbody body = part.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        yield return MovePartWorld(part, target);
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = true;
        }
    }

    private IEnumerator MovePartWorld(MotorPart part, Vector3 target)
    {
        Vector3 start = part.transform.position;
        float elapsed = 0f;
        while (elapsed < animationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationSeconds);
            part.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
        part.transform.position = target;
    }

    private void RestoreOriginalParent(MotorPart part)
    {
        if (originalParents.TryGetValue(part.componentId, out Transform parent))
        {
            part.transform.SetParent(parent, true);
        }
    }

    private void StabilizePartsOnTable()
    {
        foreach (MotorPart part in parts)
        {
            if (part == null ||
                !originalParents.TryGetValue(part.componentId, out Transform originalParent) ||
                part.transform.parent != originalParent)
            {
                continue;
            }

            Rigidbody body = part.GetComponent<Rigidbody>();
            if (body == null)
            {
                continue;
            }
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    private MotorPart FindPart(string componentId)
    {
        foreach (MotorPart part in parts)
        {
            if (part != null && part.componentId == componentId)
            {
                return part;
            }
        }
        return null;
    }

    private Vector3 NextStationPosition(string stationId, string componentId)
    {
        MotorPart part = FindPart(componentId);
        float halfHeight = 0.05f;
        if (part != null)
        {
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                halfHeight = Mathf.Max(halfHeight, renderer.bounds.extents.y);
            }
        }
        Vector2 offset = StationOffset(stationId, componentId);
        Vector3 center = StationCenter(stationId);
        return center + new Vector3(offset.x, halfHeight + 0.01f, offset.y);
    }

    private static Vector3 StationCenter(string stationId)
    {
        switch (stationId)
        {
            case "reuse_bin":
                return new Vector3(1.02f, 0.45f, -0.72f);
            case "repair_bin":
                return new Vector3(0.72f, 0.45f, -1.20f);
            case "replace_bin":
                return new Vector3(0.08f, 0.45f, -1.20f);
            case "recycle_bin":
                return new Vector3(-0.22f, 0.45f, -0.70f);
            default:
                return new Vector3(0.43f, 0.45f, -0.08f);
        }
    }

    private static Vector2 StationOffset(string stationId, string componentId)
    {
        if (stationId == "reuse_bin")
        {
            if (componentId == "terminal_box") return new Vector2(-0.29f, -0.13f);
            if (componentId == "rotor") return new Vector2(-0.04f, -0.13f);
            if (componentId == "housing") return new Vector2(0.24f, -0.12f);
            if (componentId == "front_cover") return new Vector2(-0.13f, 0.14f);
            if (componentId == "rear_cover") return new Vector2(0.13f, 0.14f);
        }
        if (stationId == "repair_bin")
        {
            if (componentId == "stator") return new Vector2(-0.14f, 0f);
            if (componentId == "shaft") return new Vector2(0.15f, 0f);
        }
        if (stationId == "replace_bin")
        {
            if (componentId == "bearing_front") return new Vector2(-0.12f, 0f);
            if (componentId == "bearing_rear") return new Vector2(0.12f, 0f);
        }
        if (stationId == "recycle_bin")
        {
            if (componentId == "fan") return new Vector2(-0.12f, 0f);
            if (componentId == "bolts") return new Vector2(0.12f, 0f);
        }
        return Vector2.zero;
    }
}
