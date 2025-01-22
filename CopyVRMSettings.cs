using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRM;

public class CopyVRMSettings : MonoBehaviour
{
    [SerializeField]
    VRMMeta source;
    [SerializeField]
    VRMMeta[] targets;

    [SerializeField]
    bool skipCopyVersion = true;
    [SerializeField]
    bool skipCopyThumbnail = false;
    [SerializeField]
    bool deactiveSource = true;

    [ContextMenu("Copy")]
    public void Copy() {
        debugLog = "";
        DebugLog("Start Copy");

        bool srcActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(true);
        DebugLog("Set Active source GameObject", 1);

        foreach (var target in targets) {
            CopyAllVRM(source, target);
        }

        source.gameObject.SetActive(srcActive && !deactiveSource);

        DebugLog("End Copy");
    }

    VRMSpringBone[] GetSprings(VRMMeta rootVRM) {
        var rootGO = rootVRM.gameObject;
        return rootGO.GetComponentsInChildren<VRMSpringBone>();
    }

    VRMSpringBoneColliderGroup[] GetColliders(VRMMeta rootVRM) {
        var rootGO = rootVRM.gameObject;
        return rootGO.GetComponentsInChildren<VRMSpringBoneColliderGroup>();
    }

    void CopyAllVRM(VRMMeta srcVRM, VRMMeta targetVRM) {
        DebugLog($"Copy to {targetVRM.name}", 1);

        var srcColliders = GetColliders(srcVRM);
        foreach (var srcCollider in srcColliders) {
            CopyCollider(srcCollider, srcVRM, targetVRM);
        }

        var srcSprings = GetSprings(srcVRM);
        foreach (var srcSpring in srcSprings) {
            CopySpring(srcSpring, srcVRM, targetVRM);
        }
        
        CopyVRMMteat(srcVRM, targetVRM);
    }

    void CopyVRMMteat(VRMMeta srcVRM, VRMMeta targetVRM) {
        DebugLog("Copy VRM Meta to" + targetVRM.name, 1);
        foreach (var field in typeof(VRMMeta).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            if (field.IsStatic) continue;
            field.SetValue(targetVRM, field.GetValue(srcVRM));
        }

        if (skipCopyVersion) {
            targetVRM.Meta.Version = "";
        }
        if (skipCopyThumbnail) {
            targetVRM.Meta.Thumbnail = null;
        }

        DebugLog("Copy FirstPerson to" + targetVRM.name, 1);
        VRMFirstPerson srcFirstPerson = srcVRM.GetComponent<VRMFirstPerson>();
        VRMFirstPerson targetFirstPerson = targetVRM.GetComponent<VRMFirstPerson>();
        targetFirstPerson.FirstPersonBone = FindWithRelativePath(srcVRM.transform, srcFirstPerson.FirstPersonBone, targetVRM.transform);
        targetFirstPerson.FirstPersonOffset = srcFirstPerson.FirstPersonOffset;

        DebugLog("Copy LookAtHead to" + targetVRM.name, 1);
        VRMLookAtHead srcLookAtHead = srcVRM.GetComponent<VRMLookAtHead>();
        VRMLookAtHead targetLookAtHead = targetVRM.GetComponent<VRMLookAtHead>();
        foreach (var field in typeof(VRMLookAtHead).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            if (field.IsStatic) continue;
            field.SetValue(targetLookAtHead, field.GetValue(srcLookAtHead));
        }
        targetLookAtHead.Head = FindWithRelativePath(srcVRM.transform, srcLookAtHead.Head, targetVRM.transform);
    }

    void CopySpring(VRMSpringBone srcSpring, VRMMeta srcVRM, VRMMeta targetVRM) {
        DebugLog("Copy Spring to" + targetVRM.name, 1);
        Transform targetSpringTr = FindWithRelativePath(srcVRM.transform, srcSpring.transform, targetVRM.transform);
        if (!targetSpringTr) {
            DebugLog("Spring bone not found: " + srcSpring.name, 2);
            targetSpringTr = new GameObject(srcSpring.name).transform;
            targetSpringTr.SetParent(targetVRM.transform);
        }
        VRMSpringBone targetSpring = targetSpringTr.GetComponent<VRMSpringBone>();
        if (!targetSpring) {
            DebugLog("Spring not found: " + srcSpring.name, 2);
            targetSpring = targetSpringTr.gameObject.AddComponent<VRMSpringBone>();
        }

        foreach (var field in typeof(VRMSpringBone).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            if (field.IsStatic) continue;
            field.SetValue(targetSpring, field.GetValue(srcSpring));
        }

        targetSpring.m_center = FindWithRelativePath(srcVRM.transform, srcSpring.m_center, targetVRM.transform);

        targetSpring.RootBones = new List<Transform>();
        for (int i = 0; i < srcSpring.RootBones.Count; i++) {
            targetSpring.RootBones.Add(FindWithRelativePath(srcVRM.transform, srcSpring.RootBones[i], targetVRM.transform));
        }

        targetSpring.ColliderGroups = new VRMSpringBoneColliderGroup[srcSpring.ColliderGroups.Length];
        for (int i = 0; i < srcSpring.ColliderGroups.Length; i++) {
            Transform colliderBone = FindWithRelativePath(srcVRM.transform, srcSpring.ColliderGroups[i].transform, targetVRM.transform);
            if (!colliderBone) {
                continue;
            }
            targetSpring.ColliderGroups[i] = colliderBone.GetComponent<VRMSpringBoneColliderGroup>();
        }
    }

    void CopyCollider(VRMSpringBoneColliderGroup srcCollider, VRMMeta srcVRM, VRMMeta targetVRM) {
        DebugLog("Copy Collider to" + targetVRM.name, 1);
        Transform targetColliderTr = FindWithRelativePath(srcVRM.transform, srcCollider.transform, targetVRM.transform);
        if (!targetColliderTr) {
            DebugLog("Collider bone not found: " + srcCollider.name, 2);
            return;
        }
        VRMSpringBoneColliderGroup targetCollider = targetColliderTr.GetComponent<VRMSpringBoneColliderGroup>();
        if (!targetCollider) {
            DebugLog("Collider not found: " + srcCollider.name, 2);
            targetCollider = targetColliderTr.gameObject.AddComponent<VRMSpringBoneColliderGroup>();
        }

        foreach (var field in typeof(VRMSpringBoneColliderGroup).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            if (field.IsStatic) continue;
            field.SetValue(targetCollider, field.GetValue(srcCollider));
        }
    }


    Transform FindWithRelativePath(Transform srcRoot, Transform srcEnd, Transform targetRoot) {
        if (!srcEnd) return null;
        return targetRoot.Find(GetObjectPath(srcRoot, srcEnd));
    }

    string GetObjectPath(Transform root, Transform find) {
        string path = "";
        Transform point = find;
        while (point != root) {
            if (path == "") {
                path = point.name;
            } else {
                path = point.name + "/" + path;
            }
            point = point.parent;
        }
        // DebugLog(path);
        return path;
    }



    [SerializeField]
    bool debugTexts = true;
    [SerializeField,TextArea(3, 10)]
    string debugLog = "";

    void DebugLog(string msg, int indent = 0) {
        if (debugTexts) {
            debugLog += new string(' ', indent*4) + msg + "\n";
        }
    }
}
