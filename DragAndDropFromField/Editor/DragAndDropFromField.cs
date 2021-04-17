using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class DragAndDropFromField {
	enum ObjectFieldValidatorOptions { None = 0x0, ExactObjectTypeValidation = 0x1 }
	delegate UnityEngine.Object ObjectFieldValidator(UnityEngine.Object[] references, Type objType, SerializedProperty property, ObjectFieldValidatorOptions options);

	static HarmonyInstance harmony;

	static void StartDrag(params UnityEngine.Object[] toDrag) {
		if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0) return;
		if (toDrag == null) return;

		var objs = new List<UnityEngine.Object>(toDrag);
		objs.RemoveAll(p => p == null);
		toDrag = objs.ToArray();
		if (toDrag.Length == 0) return;

		var instanceIDs = new List<int>();
		foreach (var elem in toDrag) instanceIDs.Add(elem.GetInstanceID());

		DragAndDrop.PrepareStartDrag();
		DragAndDrop.objectReferences = toDrag;
		DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
		DragAndDrop.StartDrag(toDrag.Length == 1 ? ObjectNames.GetDragAndDropTitle(toDrag[0]) : "<Multiple>");
	}

	[InitializeOnLoadMethod]
	static void Init() {
		ThreadPool.QueueUserWorkItem((_) => Patch());
	}

	static void Patch() {
		MethodInfo doObjectFieldMethod =
			typeof(UnityEditor.EditorGUI).GetMethods(BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public)
			.Where(m => m.Name == "DoObjectField") // Get the functions with this name
			.OrderByDescending(m => m.GetParameters().Length) // Filter to get the specific function we want
			.FirstOrDefault();

		if (doObjectFieldMethod == null) {
			Debug.LogWarning("Can't find the method UnityEditor.EditorGUI.DoObjectField(). Patching won't be done.");
			return;
		}

		if (harmony == null) harmony = HarmonyInstance.Create("com.Nukefist.Ashkatchap.DragAndDropFromField");
		else harmony.UnpatchAll();

		harmony.Patch(doObjectFieldMethod, prefix: new HarmonyMethod(typeof(DragAndDropFromField), "DoObjectFieldReplacement"));
	}

	static UnityEngine.Object preparedToDrag;
#if UNITY_2020_1_OR_NEWER
	static void DoObjectFieldReplacement(Rect position, Rect dropRect, int id, UnityEngine.Object obj, UnityEngine.Object objBeingEdited, Type objType, SerializedProperty property, ObjectFieldValidator validator, bool allowSceneObjects, GUIStyle style) {
#else
	static void DoObjectFieldReplacement(Rect position, Rect dropRect, int id, UnityEngine.Object obj, Type objType, SerializedProperty property, ObjectFieldValidator validator, bool allowSceneObjects, GUIStyle style) {
#endif
		if (!position.Contains(Event.current.mousePosition)) return;
		if (Event.current.button != 0 && Event.current.button != 1) return;
		var targetObj = obj != null ? obj : property != null ? property.objectReferenceValue : null;
		if (Event.current.type == EventType.MouseDown) {
			preparedToDrag = targetObj;
		}
		else if (Event.current.type == EventType.MouseDrag) {
			if (targetObj == preparedToDrag && targetObj != null) {
				StartDrag(targetObj);
				preparedToDrag = null;
			}
		}
	}
}
