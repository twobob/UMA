#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace UMA.Editors
{
	[CustomEditor(typeof(SlotDataAsset))]
    public class SlotDataAssetInspector : Editor
    {

        [MenuItem("Assets/Create/UMA/Core/Custom Slot Asset")]
        public static void CreateCustomSlotAssetMenuItem()
        {
        	CustomAssetUtility.CreateAsset<SlotDataAsset>("", true, "Custom");
        }

		//allow for delayed saving so typing in a field does not trigger save with every keystroke
		private float lastActionTime = 0;
		private bool doSave = false;

		void OnEnable()
		{
			EditorApplication.update += DoDelayedSave;
		}

		void OnDestroy()
		{
			EditorApplication.update -= DoDelayedSave;
		}

		void DoDelayedSave()
		{
			if (doSave && Time.realtimeSinceStartup > (lastActionTime + 0.5f))
			{
				doSave = false;
				Debug.Log("Saved SlotDataAsset lastActionTime = " + lastActionTime + " realTime = " + Time.realtimeSinceStartup);
				lastActionTime = Time.realtimeSinceStartup;
				EditorUtility.SetDirty(target);
				AssetDatabase.SaveAssets();
			}
		}
		public override void OnInspectorGUI()
        {
			if (lastActionTime == 0)
				lastActionTime = Time.realtimeSinceStartup;

			EditorGUI.BeginChangeCheck();
			base.OnInspectorGUI();
			if (EditorGUI.EndChangeCheck())
			{
				lastActionTime = Time.realtimeSinceStartup;
				doSave = true;
			}

			foreach (var t in targets)
			{
				var slotDataAsset = t as SlotDataAsset;
				if (slotDataAsset != null)
				{
					if (slotDataAsset.animatedBoneHashes.Length != slotDataAsset.animatedBoneNames.Length)
					{
						slotDataAsset.animatedBoneHashes = new int[slotDataAsset.animatedBoneNames.Length];
						for (int i = 0; i < slotDataAsset.animatedBoneNames.Length; i++)
						{
							slotDataAsset.animatedBoneHashes[i] = UMASkeleton.StringToHash(slotDataAsset.animatedBoneNames[i]);
						}
						//DelayedSave here too?
						EditorUtility.SetDirty(slotDataAsset);
						AssetDatabase.SaveAssets();
					}
				}
			}

            GUILayout.Space(20);
            Rect updateDropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(updateDropArea, "Drag SkinnedMeshRenderers here to update the slot meshData.");
            GUILayout.Space(10);
            UpdateSlotDropAreaGUI(updateDropArea);

			GUILayout.Space(10);
			Rect boneDropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
			GUI.Box(boneDropArea, "Drag Bone Transforms here to add their names to the Animated Bone Names.\nSo the power tools will preserve them!");
			GUILayout.Space(10);
			AnimatedBoneDropAreaGUI(boneDropArea);

			GUILayout.Space(10);
			if (GUILayout.Button("Spawn MeshData in Scene", GUILayout.Height(50)))
			{
				SpawnMeshFromSlotData();
			}
        }

        private void AnimatedBoneDropAreaGUI(Rect dropArea)
        {
            GameObject obj = DropAreaGUI(dropArea);
            if (obj != null)
                AddAnimatedBone(obj.name);
        }

        private void UpdateSlotDropAreaGUI(Rect dropArea)
        {
            GameObject obj = DropAreaGUI(dropArea);
            if (obj != null)
            {
                SkinnedMeshRenderer skinnedMesh = obj.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMesh != null)
                {
                    Debug.Log("Updating SlotDataAsset with SkinnedMeshRenderer...");
                    UpdateSlotData(skinnedMesh);
                    Debug.Log("Update Complete!");
                }
                else
                    EditorUtility.DisplayDialog("Error", "No skinned mesh renderer found!", "Ok");
            }
                
        }

        private GameObject DropAreaGUI(Rect dropArea)
		{
			var evt = Event.current;

			if (evt.type == EventType.DragUpdated)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				}
			}

			if (evt.type == EventType.DragPerform)
			{
				if (dropArea.Contains(evt.mousePosition))
				{
					DragAndDrop.AcceptDrag();
					UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
					for (int i = 0; i < draggedObjects.Length; i++)
					{
						if (draggedObjects[i])
						{
							var go = draggedObjects[i] as GameObject;
							if (go != null)
							{
                                return go;
							}
						}
					}
					AssetDatabase.SaveAssets();
				}
			}
            return null;
		}

		private void AddAnimatedBone(string animatedBone)
		{
			var hash = UMASkeleton.StringToHash(animatedBone);
			foreach (var t in targets)
			{
				var slotDataAsset = t as SlotDataAsset;
				if (slotDataAsset != null)
				{
					ArrayUtility.Add(ref slotDataAsset.animatedBoneNames, animatedBone);
					ArrayUtility.Add(ref slotDataAsset.animatedBoneHashes, hash);
					EditorUtility.SetDirty(slotDataAsset);
				}
			}			
		}

        private void UpdateSlotData(SkinnedMeshRenderer skinnedMesh)
        {
            SlotDataAsset slot = target as SlotDataAsset;

            string existingRootBone = slot.meshData.RootBoneName;

            UMASlotProcessingUtil.UpdateSlotData(slot, skinnedMesh, slot.material, null, existingRootBone);
        }

		[ExecuteInEditMode]
		private void SpawnMeshFromSlotData()
		{
			SlotDataAsset slotDataAsset = target as SlotDataAsset;

			//Create the gameobjects and skinned mesh renderer
			GameObject meshObj = new GameObject(slotDataAsset.slotName );
			GameObject rendererObj = new GameObject(slotDataAsset.slotName, typeof(SkinnedMeshRenderer));
			SkinnedMeshRenderer renderer = rendererObj.GetComponent<SkinnedMeshRenderer>();
			rendererObj.transform.SetParent(meshObj.transform);

			renderer.sharedMesh = new Mesh();

			//Set up the required Global dummy bone
			GameObject newGlobal = new GameObject("Global");
			newGlobal.transform.parent = meshObj.transform;
			newGlobal.transform.localPosition = Vector3.zero;
			newGlobal.transform.localRotation = Quaternion.Euler(0f, 0f, -90f); 
			UMASkeleton skeleton = new UMASkeleton(newGlobal.transform);

			//Build the skeleton bones
			for(int i = 0; i < slotDataAsset.meshData.umaBoneCount; i++)
			{
				skeleton.EnsureBone(slotDataAsset.meshData.umaBones[i]);
			}
			skeleton.EnsureBoneHierarchy();

			//Copy the data to the new skinned mesh renderer in the scene
			Mesh mesh = renderer.sharedMesh;
			mesh.subMeshCount = 1;
			mesh.triangles = new int[0];
			mesh.vertices = slotDataAsset.meshData.vertices;
			mesh.boneWeights = UMABoneWeight.Convert(slotDataAsset.meshData.boneWeights);
			mesh.normals = slotDataAsset.meshData.normals;
			mesh.tangents = slotDataAsset.meshData.tangents;
			mesh.uv = slotDataAsset.meshData.uv;
			mesh.uv2 = slotDataAsset.meshData.uv2;
			mesh.uv3 = slotDataAsset.meshData.uv3;
			mesh.uv4 = slotDataAsset.meshData.uv4;
			mesh.colors32 = slotDataAsset.meshData.colors32;
			mesh.bindposes = slotDataAsset.meshData.bindPoses;

			var subMeshCount = slotDataAsset.meshData.submeshes.Length;
			mesh.subMeshCount = subMeshCount;
			for (int i = 0; i < subMeshCount; i++)
			{
				mesh.SetTriangles(slotDataAsset.meshData.submeshes[i].triangles, i);
			}

			renderer.bones = skeleton.HashesToTransforms(slotDataAsset.meshData.boneNameHashes);
			renderer.rootBone = slotDataAsset.meshData.rootBone;

			mesh.RecalculateBounds();
			renderer.sharedMesh = mesh;
		}
    }
}
#endif
