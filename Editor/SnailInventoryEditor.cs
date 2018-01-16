
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using VRCSDK2;

[CustomEditor(typeof(SnailInventory))]
public class SnailInventoryEditor : Editor {
    public class SnailInventoryComponent : MonoBehaviour { }
    public static readonly string INVENTORY_NAME = "InventorySystem";
    public static readonly string TEMPLATE_PATH = "Assets/VRCSDK/Examples/Sample Assets/Animation/AvatarControllerTemplate.controller";
    
    public static readonly AnimationCurve OFF = new AnimationCurve(new Keyframe[]{
            new Keyframe(0,0),
            new Keyframe(1,0),
        });
    public static readonly AnimationCurve ON = new AnimationCurve(new Keyframe[]{
            new Keyframe(0,1),
            new Keyframe(1,1),
        });
    public static string GetAnimatorPath(GameObject obj, GameObject root = null)
    {
        Transform cur = obj.transform;
        string path = "";
        do
        {
            
            if (root == null) {
                if (cur.GetComponent<VRCSDK2.VRC_AvatarDescriptor>() != null)
                    return path;
            } else {
                if (cur == root.transform)
                    return path;
            }

            if (path.Length > 0)
                path = cur.name + "/" + path;
            else
                path = cur.name;
            cur = cur.parent;
        } while (cur != null);
        return null;
    }
    public static void WriteAsset(Object asset, string path)
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    SnailInventory obj;
    public void OnEnable()
    {
        obj = (SnailInventory)target;
    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Generate inventory system"))
            GenerateInventory();
    }
    
    private void RemoveInventorySystem()
    {
        List<Transform> toDelete = new List<Transform>();
        walkTree(obj.transform, (child) => {
            if (child.name.StartsWith("SI_"))
                toDelete.Add(child);
        });

        toDelete.ForEach(cur =>
        {
            Transform newParent = cur.parent;
            // Move all of our children up.
            foreach (Transform child in cur)
            {
                child.parent = newParent;
            }
            DestroyImmediate(cur.gameObject);
        });
    }
    private void walkTree(Transform root, System.Action<Transform> cb ) {
        for(int i = root.childCount-1; i>=0; --i)
            walkTree(root.GetChild(i), cb);
        cb(root);
    }
    private GameObject insertParent(GameObject obj, string suffix)
    {
        GameObject g = new GameObject("SI_" + suffix);
        g.transform.parent = obj.transform.parent;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localPosition = Vector3.zero;
        g.transform.localScale = Vector3.one;

        obj.transform.parent = g.transform;
        return g;
    }

    private void GenerateInventory()
    {
        RemoveInventorySystem();

        // Setup the legacy enabler/disablers. 
        Dictionary<string, GameObject> enablers = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> disablers = new Dictionary<string, GameObject>();
        for (int i = 0; i < obj.Items.Count; i++)
        {
            var item = obj.Items[i];
            item.Object.SetActive(false);

            GameObject enabler = insertParent(item.Object, "enable_" + item.Name);
            setupLegacyIsActiveAnimation(enabler, item.Object, true);
            enablers.Add(item.Name, enabler);

            GameObject disabler = insertParent(enabler, "disable_" + item.Name);
            setupLegacyIsActiveAnimation(disabler, item.Object, false);
            disablers.Add(item.Name, disabler);
        }

        // Setup the emote animations.
        Dictionary<string, AnimationClip> overrides = new Dictionary<string, AnimationClip>();
        for (int i = 0; i < obj.Items.Count; i++)
        {
            // enable item [i], disable all other items.
            var item = obj.Items[i];
            AnimationClip clip = new AnimationClip();
            if (obj.SwitchAnimation != null)
                mergeAnimationClips(clip, obj.SwitchAnimation);
            addLegacyAnimationStarter(clip, enablers[item.Name]);
            for (int j = 0; j < obj.Items.Count; j++)
            {
                if (i == j) continue;
                addLegacyAnimationStarter(clip, disablers[obj.Items[j].Name]);
            }
            
            if(item.Overrides != null)
            {
                List<KeyValuePair<AnimationClip, AnimationClip>> list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                item.Overrides.GetOverrides(list);
                list.ForEach(pair =>
                {
                    if (pair.Value == null) return;
                    AnimationClip existing;
                    if (!overrides.TryGetValue(pair.Key.name, out existing))
                    {
                        existing = new AnimationClip();
                        existing.name = pair.Key.name + "Merged";
                        WriteAsset(existing, "Assets/tmp/" + existing.name + ".anim");
                        overrides.Add(pair.Key.name, existing);
                    }
                    mergeAnimationClips(existing, pair.Value);
                });
            }

            WriteAsset(clip, "Assets/tmp/" + item.Name + "_Inv.anim");
            overrides.Add("EMOTE" + (i + 1), clip);
        }

        setupOverrides(overrides);
    }
    private void mergeAnimationClips(AnimationClip start, AnimationClip additional)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(additional))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(additional, binding);
            start.SetCurve(binding.path, binding.type, binding.propertyName, curve);
        }
    }
    private void addLegacyAnimationStarter(AnimationClip clip, GameObject target)
    {

        clip.SetCurve(
            GetAnimatorPath(target),
            typeof(Animation),
            "m_PlayAutomatically",
            ON);

        AnimationCurve curve = new AnimationCurve();

        for (int i = 0; i < 12; i++)
        {
            Keyframe frame = new Keyframe(i / 12f, i % 2);
            frame.outTangent = Mathf.Infinity;
            curve.AddKey(frame);
        }

        clip.SetCurve(
            GetAnimatorPath(target),
            typeof(GameObject),
            "m_IsActive",
            curve
            );
    }

    private void setupLegacyIsActiveAnimation(GameObject root, GameObject target, bool isActive)
    {
        AnimationClip clip = new AnimationClip();
        clip.legacy = true;
        clip.name = target.name + "_" + isActive;
        clip.SetCurve(GetAnimatorPath(target, root),
            typeof(GameObject),
            "m_IsActive",
            isActive ? ON : OFF);
        WriteAsset(clip, "Assets/tmp/" + clip.name + ".anim");

        Animation anim = root.AddComponent<Animation>();
        anim.playAutomatically = false;
        anim.clip = clip;
    }

    private void setupOverrides(Dictionary<string, AnimationClip> items)
    {
        AnimatorOverrideController o = new AnimatorOverrideController();
        o.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TEMPLATE_PATH);
        List<KeyValuePair<AnimationClip, AnimationClip>> list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        o.GetOverrides(list);

        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        foreach (var clip in list)
        {
            AnimationClip c;
            if (items.TryGetValue(clip.Key.name, out c)) {
                overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip.Key, c));
            }
        }

        o.ApplyOverrides(overrides);
        WriteAsset(o, "Assets/tmp/Overrides.overridecontroller");

        obj.GetComponent<VRC_AvatarDescriptor>().CustomStandingAnims = o;
        obj.GetComponent<VRC_AvatarDescriptor>().CustomStandingAnims = o;
        obj.GetComponent<Animator>().runtimeAnimatorController = o;
    }
}

