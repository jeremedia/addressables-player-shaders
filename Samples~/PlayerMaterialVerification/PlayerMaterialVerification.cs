using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AddressablesPlayerShaders.Samples
{
    public sealed class PlayerMaterialVerification : MonoBehaviour
    {
        [Tooltip("Leave empty if the catalog was loaded by the host application.")]
        public string catalogUrl;
        public string materialAddress;
        [Tooltip("Direct Player asset reference. Do not put this verification scene in an Addressable bundle.")]
        public Shader expectedPlayerShader;
        public Renderer displayRenderer;

        AsyncOperationHandle<Material> materialHandle;
        AsyncOperationHandle<IResourceLocator> catalogHandle;
        Material previousMaterial;
        bool assigned;
        string status = "Waiting";

        IEnumerator Start()
        {
            if (Application.isEditor) { Fail("Run this verification in a standalone Player, not Editor Play Mode."); yield break; }
            if (expectedPlayerShader == null || displayRenderer == null || string.IsNullOrWhiteSpace(materialAddress))
            { Fail("Assign expected shader, display renderer and material address."); yield break; }
            if (!string.IsNullOrWhiteSpace(catalogUrl))
            {
                status = "Loading test catalog";
                catalogHandle = Addressables.LoadContentCatalogAsync(catalogUrl, false);
                yield return catalogHandle;
                if (catalogHandle.Status != AsyncOperationStatus.Succeeded) { Fail("Catalog failed: " + catalogHandle.OperationException); yield break; }
            }
            status = "Loading material";
            materialHandle = Addressables.LoadAssetAsync<Material>(materialAddress);
            yield return materialHandle;
            if (materialHandle.Status != AsyncOperationStatus.Succeeded) { Fail("Material failed: " + materialHandle.OperationException); yield break; }
            var material = materialHandle.Result;
            if (material == null || material.shader != expectedPlayerShader)
            { Fail("Loaded material does not reference the expected Player shader object. No rebinding was performed."); yield break; }
            if (!material.shader.isSupported) { Fail("Player shader reports unsupported on this device."); yield break; }
            previousMaterial = displayRenderer.sharedMaterial;
            displayRenderer.sharedMaterial = material;
            assigned = true;
            status = "PASS: Player shader object identity matches. Inspect rendering and strict variant matching separately.";
            Debug.Log("APS: " + status + " Material=" + materialAddress + " Shader=" + material.shader.name);
        }

        void Fail(string reason) { status = "FAIL: " + reason; Debug.LogError("APS: " + status); }

        void OnGUI()
        {
            // High-contrast textual status; never make color or pink rendering the only failure signal.
            GUI.Box(new Rect(12, 12, Mathf.Min(Screen.width - 24, 760), 86), "");
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 18 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(24, 20, Mathf.Min(Screen.width - 48, 736), 72), status, style);
        }

        void OnDestroy()
        {
            if (assigned && displayRenderer != null) displayRenderer.sharedMaterial = previousMaterial;
            if (materialHandle.IsValid()) Addressables.Release(materialHandle);
            if (catalogHandle.IsValid()) Addressables.Release(catalogHandle);
        }
    }
}
