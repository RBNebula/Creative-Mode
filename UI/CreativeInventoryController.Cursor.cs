using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace MineMogul.CreativeMode.UI;

internal sealed partial class CreativeInventoryController
{
    private void OnCreativeItemPressed(ShopItem shopItem)
    {
        if (!_creativeModeEnabled || shopItem == null || shopItem.Definition == null)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            bool ctrlSuccess = TryBeginPendingCursorItem(shopItem.Definition, fillToMaxStack: true);
            if (!ctrlSuccess)
            {
                _logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to ctrl-pick creative item '{shopItem.GetName()}'.");
            }

            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            bool shiftedSuccess = TryGiveItem(shopItem.Definition);
            if (!shiftedSuccess)
            {
                _logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to shift-give creative item '{shopItem.GetName()}'.");
            }

            return;
        }

        if (TryBeginPendingCursorItem(shopItem.Definition))
        {
            return;
        }

        bool success = TryGiveItem(shopItem.Definition);
        if (!success)
        {
            _logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to spawn creative item '{shopItem.GetName()}'.");
        }
    }

    internal bool HasPendingCursorItem()
    {
        return _pendingCursorTool != null;
    }

    internal bool TryPlacePendingCursorItem(InventorySlotUI slotUi)
    {
        if (_pendingCursorTool == null || slotUi == null)
        {
            return false;
        }

        PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>();
        if (inventory == null || slotUi.SlotIndex < 0 || slotUi.SlotIndex >= inventory.Items.Count)
        {
            return false;
        }

        BaseHeldTool existingTool = inventory.Items[slotUi.SlotIndex];
        if (existingTool == null)
        {
            BaseHeldTool toolToPlace = _pendingCursorTool;
            _pendingCursorTool = null;
            HideDragGhost();
            bool added = inventory.TryAddToInventory(toolToPlace, slotUi.SlotIndex);
            if (!added)
            {
                _pendingCursorTool = toolToPlace;
                UpdatePendingCursorGhost();
            }

            return added;
        }

        if (!TryMergeIntoExistingTool(inventory, existingTool))
        {
            UpdatePendingCursorGhost();
            return false;
        }

        return true;
    }

    private bool TryGiveItem(ShopItemDefinition definition)
    {
        PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            return false;
        }

        if (definition.BuildingInventoryDefinition != null)
        {
            return TryGiveBuildingTool(inventory, definition.BuildingInventoryDefinition);
        }

        if (definition.PrefabToSpawn == null)
        {
            return false;
        }

        BaseHeldTool heldToolPrefab = definition.PrefabToSpawn.GetComponent<BaseHeldTool>();
        if (heldToolPrefab != null)
        {
            return TryGiveHeldTool(inventory, heldToolPrefab);
        }

        GameObject spawned = Object.Instantiate(definition.PrefabToSpawn);
        PlaceSpawnedObject(spawned);
        return true;
    }

    private bool TryGiveBuildingTool(PlayerInventory inventory, BuildingInventoryDefinition buildingDefinition)
    {
        BuildingManager buildingManager = Singleton<BuildingManager>.Instance;
        if (buildingManager == null || buildingManager.BuildingToolPrefab == null)
        {
            return false;
        }

        ToolBuilder tool = Object.Instantiate(buildingManager.BuildingToolPrefab);
        tool.Definition = buildingDefinition;
        tool.Setup();
        tool.Quantity = 1;

        if (inventory.TryAddToInventory(tool))
        {
            return true;
        }

        Object.Destroy(tool.gameObject);
        return TrySpawnBuildingCrate(buildingDefinition);
    }

    private bool TryGiveHeldTool(PlayerInventory inventory, BaseHeldTool toolPrefab)
    {
        BaseHeldTool tool = Object.Instantiate(toolPrefab);
        if (inventory.TryAddToInventory(tool))
        {
            return true;
        }

        PlaceSpawnedObject(tool.gameObject);
        return true;
    }

    private bool TrySpawnBuildingCrate(BuildingInventoryDefinition buildingDefinition)
    {
        BuildingCrate cratePrefab = null;
        if (buildingDefinition.PackedPrefab != null)
        {
            cratePrefab = buildingDefinition.PackedPrefab;
        }
        else
        {
            BuildingManager buildingManager = Singleton<BuildingManager>.Instance;
            if (buildingManager != null)
            {
                cratePrefab = buildingManager.BuildingCratePrefab;
            }
        }

        if (cratePrefab == null)
        {
            return false;
        }

        BuildingCrate crate = Object.Instantiate(cratePrefab);
        crate.Definition = buildingDefinition;
        crate.Quantity = 1;
        PlaceSpawnedObject(crate.gameObject);
        return true;
    }

    private void PlaceSpawnedObject(GameObject spawnedObject)
    {
        if (spawnedObject == null)
        {
            return;
        }

        Transform spawnTransform = ResolveSpawnTransform();
        if (spawnTransform != null)
        {
            spawnedObject.transform.position = spawnTransform.position;
            spawnedObject.transform.rotation = spawnTransform.rotation;
        }

        Rigidbody body = spawnedObject.GetComponentInChildren<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = false;
            body.linearVelocity = spawnTransform != null ? spawnTransform.forward * 4f : Vector3.zero;
            body.angularVelocity = Random.insideUnitSphere;
        }
    }

    private bool TryBeginPendingCursorItem(ShopItemDefinition definition, bool fillToMaxStack = false)
    {
        if (definition == null)
        {
            return false;
        }

        if (TryStackPendingCursorItem(definition, fillToMaxStack))
        {
            UpdatePendingCursorGhost();
            return true;
        }

        if (!TryCreatePendingCursorTool(definition, out BaseHeldTool tool))
        {
            return false;
        }

        if (fillToMaxStack)
        {
            tool.Quantity = GetDesiredPendingQuantity(tool, fillToMaxStack: true);
        }

        ReplacePendingCursorTool(tool);
        UpdatePendingCursorGhost();
        return true;
    }

    private bool TryStackPendingCursorItem(ShopItemDefinition definition, bool fillToMaxStack)
    {
        if (_pendingCursorTool == null || definition == null)
        {
            return false;
        }

        if (!IsSameDefinitionAsPending(definition))
        {
            return false;
        }

        int desiredQuantity = GetDesiredPendingQuantity(_pendingCursorTool, fillToMaxStack);
        if (_pendingCursorTool.Quantity >= desiredQuantity)
        {
            return true;
        }

        if (fillToMaxStack)
        {
            _pendingCursorTool.Quantity = desiredQuantity;
            return true;
        }

        _pendingCursorTool.Quantity = Mathf.Min(desiredQuantity, _pendingCursorTool.Quantity + 1);
        return true;
    }

    private static int GetDesiredPendingQuantity(BaseHeldTool tool, bool fillToMaxStack)
    {
        if (tool == null)
        {
            return 1;
        }

        int maxAmount = Mathf.Max(1, tool.MaxAmount);
        if (!fillToMaxStack)
        {
            return maxAmount;
        }

        return maxAmount;
    }

    private void TryAddPendingCursorItemFromScroll()
    {
        if (_pendingCursorTool == null)
        {
            return;
        }

        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        int maxAmount = Mathf.Max(1, _pendingCursorTool.MaxAmount);
        int notches = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(scrollDelta)));

        if (scrollDelta > 0f)
        {
            if (_pendingCursorTool.Quantity >= maxAmount)
            {
                return;
            }

            _pendingCursorTool.Quantity = Mathf.Min(maxAmount, _pendingCursorTool.Quantity + notches);
            UpdatePendingCursorGhost();
            return;
        }

        _pendingCursorTool.Quantity = Mathf.Max(0, _pendingCursorTool.Quantity - notches);
        if (_pendingCursorTool.Quantity <= 0)
        {
            CancelPendingCursorItem();
            return;
        }

        UpdatePendingCursorGhost();
    }

    private bool IsSameDefinitionAsPending(ShopItemDefinition definition)
    {
        if (_pendingCursorTool == null || definition == null)
        {
            return false;
        }

        if (_pendingCursorTool is ToolBuilder pendingBuilder)
        {
            return definition.BuildingInventoryDefinition != null &&
                pendingBuilder.Definition == definition.BuildingInventoryDefinition;
        }

        if (definition.PrefabToSpawn == null)
        {
            return false;
        }

        BaseHeldTool prefabTool = definition.PrefabToSpawn.GetComponent<BaseHeldTool>();
        if (prefabTool == null)
        {
            return false;
        }

        return AreSameHeldToolKind(_pendingCursorTool, prefabTool);
    }

    private bool TryCreatePendingCursorTool(ShopItemDefinition definition, out BaseHeldTool tool)
    {
        tool = null;
        if (definition == null)
        {
            return false;
        }

        if (definition.BuildingInventoryDefinition != null)
        {
            BuildingManager buildingManager = Singleton<BuildingManager>.Instance;
            if (buildingManager == null || buildingManager.BuildingToolPrefab == null)
            {
                return false;
            }

            ToolBuilder buildingTool = Object.Instantiate(buildingManager.BuildingToolPrefab);
            buildingTool.Definition = definition.BuildingInventoryDefinition;
            buildingTool.Setup();
            buildingTool.Quantity = 1;
            buildingTool.gameObject.SetActive(false);
            tool = buildingTool;
            return true;
        }

        if (definition.PrefabToSpawn == null)
        {
            return false;
        }

        BaseHeldTool heldToolPrefab = definition.PrefabToSpawn.GetComponent<BaseHeldTool>();
        if (heldToolPrefab == null)
        {
            return false;
        }

        BaseHeldTool heldTool = Object.Instantiate(heldToolPrefab);
        heldTool.Quantity = Mathf.Max(1, heldTool.Quantity);
        heldTool.gameObject.SetActive(false);
        tool = heldTool;
        return true;
    }

    private void ReplacePendingCursorTool(BaseHeldTool tool)
    {
        if (_pendingCursorTool != null && _pendingCursorTool != tool)
        {
            Object.Destroy(_pendingCursorTool.gameObject);
        }

        _pendingCursorTool = tool;
    }

    private bool TryMergeIntoExistingTool(PlayerInventory inventory, BaseHeldTool existingTool)
    {
        if (_pendingCursorTool == null || inventory == null || existingTool == null)
        {
            return false;
        }

        if (!CanMergeTools(existingTool, _pendingCursorTool))
        {
            return false;
        }

        int quantityToAdd = Mathf.Min(_pendingCursorTool.Quantity, existingTool.MaxAmount - existingTool.Quantity);
        if (quantityToAdd <= 0)
        {
            return false;
        }

        existingTool.Quantity += quantityToAdd;
        _pendingCursorTool.Quantity -= quantityToAdd;

        AccessTools.Method(typeof(PlayerInventory), "UpdateUI")?.Invoke(inventory, null);
        Singleton<SoundManager>.Instance.PlayUISound(inventory.PickupItemSound);

        if (_pendingCursorTool.Quantity <= 0)
        {
            CancelPendingCursorItem();
        }
        else
        {
            UpdatePendingCursorGhost();
        }

        return true;
    }

    private static bool CanMergeTools(BaseHeldTool existingTool, BaseHeldTool pendingTool)
    {
        if (existingTool == null || pendingTool == null || existingTool.MaxAmount <= existingTool.Quantity)
        {
            return false;
        }

        if (existingTool.GetType() != pendingTool.GetType())
        {
            return false;
        }

        if (existingTool is ToolBuilder existingBuilder && pendingTool is ToolBuilder pendingBuilder)
        {
            return existingBuilder.Definition == pendingBuilder.Definition;
        }

        return AreSameHeldToolKind(existingTool, pendingTool);
    }

    private void UpdatePendingCursorGhost()
    {
        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        if (inventoryUi == null || inventoryUi.DragGhostIcon == null || inventoryUi.DragGhostImage == null || inventoryUi.DragGhostAmountText == null)
        {
            return;
        }

        if (_pendingCursorTool == null || !IsInventoryVisible())
        {
            HideDragGhost();
            return;
        }

        if (_overlayCanvasRoot != null && inventoryUi.DragGhostIcon.transform.parent != _overlayCanvasRoot.transform)
        {
            if (_dragGhostOriginalParent == null)
            {
                _dragGhostOriginalParent = inventoryUi.DragGhostIcon.transform.parent;
                _dragGhostOriginalSiblingIndex = inventoryUi.DragGhostIcon.transform.GetSiblingIndex();
            }

            inventoryUi.DragGhostIcon.transform.SetParent(_overlayCanvasRoot.transform, worldPositionStays: false);
        }

        _usingCreativeDragGhost = true;
        inventoryUi.DragGhostIcon.SetActive(true);
        inventoryUi.DragGhostImage.sprite = _pendingCursorTool.GetIcon();
        inventoryUi.DragGhostImage.enabled = inventoryUi.DragGhostImage.sprite != null;
        inventoryUi.DragGhostAmountText.text = (_pendingCursorTool.Quantity > 1) ? _pendingCursorTool.Quantity.ToString() : string.Empty;
        inventoryUi.DragGhostIcon.transform.SetAsLastSibling();
        inventoryUi.DragGhostIcon.transform.position = Input.mousePosition;
    }

    private void HideDragGhost()
    {
        if (!_usingCreativeDragGhost)
        {
            return;
        }

        InventoryUIManager inventoryUi = Singleton<InventoryUIManager>.Instance;
        if (inventoryUi != null && inventoryUi.DragGhostIcon != null)
        {
            inventoryUi.DragGhostIcon.SetActive(false);

            if (_dragGhostOriginalParent != null && inventoryUi.DragGhostIcon.transform.parent != _dragGhostOriginalParent)
            {
                inventoryUi.DragGhostIcon.transform.SetParent(_dragGhostOriginalParent, worldPositionStays: false);
                if (_dragGhostOriginalSiblingIndex >= 0 && _dragGhostOriginalSiblingIndex < _dragGhostOriginalParent.childCount)
                {
                    inventoryUi.DragGhostIcon.transform.SetSiblingIndex(_dragGhostOriginalSiblingIndex);
                }
            }
        }

        _usingCreativeDragGhost = false;
        _dragGhostOriginalParent = null;
        _dragGhostOriginalSiblingIndex = -1;
    }

    private void CancelPendingCursorItem()
    {
        HideDragGhost();

        if (_pendingCursorTool != null)
        {
            Object.Destroy(_pendingCursorTool.gameObject);
            _pendingCursorTool = null;
        }
    }

    private bool IsPointerOverCreativePickupTarget()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject target = results[i].gameObject;
            if (target == null)
            {
                continue;
            }

            if (target.GetComponentInParent<CreativeInventoryItemButton>() != null)
            {
                return true;
            }

            if (target.GetComponentInParent<InventorySlotUI>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void DropPendingCursorItemToWorld()
    {
        if (_pendingCursorTool == null)
        {
            return;
        }

        BaseHeldTool toolToDrop = _pendingCursorTool;
        _pendingCursorTool = null;
        HideDragGhost();

        if (toolToDrop is ToolBuilder builder)
        {
            bool spawnedCrate = TrySpawnBuildingCrate(builder.Definition);
            Object.Destroy(toolToDrop.gameObject);
            if (!spawnedCrate)
            {
                _logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to drop pending building item '{GetHeldToolDisplayName(builder)}' into the world.");
            }

            return;
        }

        toolToDrop.gameObject.SetActive(true);
        PlaceSpawnedObject(toolToDrop.gameObject);
    }

    private Transform ResolveSpawnTransform()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null && player.PlayerCamera != null)
        {
            Transform cameraTransform = player.PlayerCamera.transform;

            GameObject fallback = new GameObject("CreativeModeSpawnAnchor");
            fallback.transform.position = cameraTransform.position + cameraTransform.forward * 1f;
            fallback.transform.rotation = cameraTransform.rotation;
            Object.Destroy(fallback, 0.1f);
            return fallback.transform;
        }

        ShopSpawnPoint[] shopSpawns = Object.FindObjectsByType<ShopSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (shopSpawns.Length > 0)
        {
            int index = Random.Range(0, shopSpawns.Length);
            return shopSpawns[index].transform;
        }

        return null;
    }

    private static bool AreSameHeldToolKind(BaseHeldTool left, BaseHeldTool right)
    {
        if (left == null || right == null || left.GetType() != right.GetType())
        {
            return false;
        }

        SavableObjectID leftId = left.GetSavableObjectID();
        SavableObjectID rightId = right.GetSavableObjectID();
        if (leftId != SavableObjectID.INVALID || rightId != SavableObjectID.INVALID)
        {
            return leftId == rightId;
        }

        return string.Equals(left.GetName(), right.GetName(), StringComparison.Ordinal);
    }

    private static string GetHeldToolDisplayName(BaseHeldTool tool)
    {
        if (tool == null)
        {
            return "Unknown";
        }

        string name = tool.GetName();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (!string.IsNullOrWhiteSpace(tool.FallbackName))
        {
            return tool.FallbackName;
        }

        return tool.GetType().Name;
    }
}
