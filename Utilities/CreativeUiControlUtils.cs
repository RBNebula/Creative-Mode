using System.Collections.Generic;
using UnityEngine;

namespace MineMogul.CreativeMode.Utilities;

internal static class CreativeUiControlUtils
{
    internal static void EnsureCursorUnlocked()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    internal static void TogglePlayerControls(bool enable, List<MonoBehaviour> disabledScripts)
    {
        GameObject playerRoot = GameObject.FindWithTag("Player");
        if (playerRoot == null && Camera.main != null)
        {
            playerRoot = Camera.main.transform.root.gameObject;
        }

        if (playerRoot == null)
        {
            if (enable)
            {
                disabledScripts.Clear();
            }

            return;
        }

        MonoBehaviour[] scripts = playerRoot.GetComponents<MonoBehaviour>();
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour script = scripts[i];
            if (script == null)
            {
                continue;
            }

            string typeName = script.GetType().Name;
            if (typeName != "PlayerController" && typeName != "PlayerInventory")
            {
                continue;
            }

            if (!enable)
            {
                if (!script.enabled)
                {
                    continue;
                }

                script.enabled = false;
                if (!disabledScripts.Contains(script))
                {
                    disabledScripts.Add(script);
                }

                continue;
            }

            if (disabledScripts.Contains(script))
            {
                script.enabled = true;
            }
        }

        if (enable)
        {
            disabledScripts.Clear();
        }
    }
}
