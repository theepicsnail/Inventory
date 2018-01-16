using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(VRCSDK2.VRC_AvatarDescriptor))]
public class SnailInventory : MonoBehaviour
{
    public AnimationClip SwitchAnimation;
    public List<SnailInventoryItem> Items;
    public void OnValidate()
    {
        if (Items.Count > 8)
        {
            Debug.LogError("Only up to 8 items.");
            return;
        }
    }
}

[System.Serializable]
public class SnailInventoryItem
{
    public string Name;
    public GameObject Object;
    public AnimatorOverrideController Overrides;
}
