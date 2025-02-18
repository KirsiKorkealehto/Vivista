﻿//#define DEBUG_UI_INPUT_MODULE

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Management;

public class SphereUIInputModule: StandaloneInputModule
{
	private new Camera camera;
	private RenderTexture uiTexture;

	private Dictionary<int, PointerEventData> pointers;
	private Dictionary<int, Vector3> directions;
	public Dictionary<int, Vector2> positions;
	private Dictionary<int, Vector2> positionResults;
	private Dictionary<int, RaycastResult> raycastResults;
	private Dictionary<int, PointerEventData.FramePressState> clickStates;
	private Dictionary<int, GameObject> previousHovers;

	public Controller leftController;
	public Controller rightController;

	public float offset;

	private const int gazeId = 1;
	public const int rightControllerId = 2;
	public const int leftControllerId = 3;


	protected override void Awake()
	{
		pointers = new Dictionary<int, PointerEventData>();
		directions = new Dictionary<int, Vector3>();
		positions = new Dictionary<int, Vector2>();
		positionResults = new Dictionary<int, Vector2>();
		raycastResults = new Dictionary<int, RaycastResult>();
		clickStates = new Dictionary<int, PointerEventData.FramePressState>();
		previousHovers = new Dictionary<int, GameObject>();

		camera = Camera.main;
		uiTexture = GetComponent<Camera>().targetTexture;
		base.Awake();
	}

	protected PointerEventData.FramePressState StateForControllerTrigger(Controller controller)
	{
		var pressed = controller.triggerPressed;
		var released = controller.triggerReleased;

		if (pressed && released)
		{
			return PointerEventData.FramePressState.PressedAndReleased;
		}
		if (pressed)
		{
			return PointerEventData.FramePressState.Pressed;
		}
		if (released)
		{
			return PointerEventData.FramePressState.Released;
		}

		return PointerEventData.FramePressState.NotChanged;
	}

	public override void Process()
	{
		GetPointerData(kMouseLeftId, out var leftData, true);
		
		leftData.Reset();

		//NOTE(Simon): There could be more than 1 inputdevice (VR controllers for example), so store them all in a list
		directions.Clear();
		if (XRSettings.isDeviceActive)
		{
			if (!VRDevices.hasNoControllers)
			{
				if (VRDevices.hasRightController)
				{
					directions.Add(rightControllerId, rightController.GetComponent<Controller>().CastRay().direction);
				}

				if (VRDevices.hasLeftController)
				{
					directions.Add(leftControllerId, leftController.GetComponent<Controller>().CastRay().direction);
				}
			}
		}
		if (Input.mousePresent)
		{
			directions.Add(kMouseLeftId, camera.ScreenPointToRay((Vector2)Input.mousePosition).direction);
		}

		positions.Clear();
		float positionOffsetPx = offset / 360 * uiTexture.width;
		foreach (var direction in directions)
		{
			positions.Add(direction.Key, new Vector2
			{
				x = (uiTexture.width * (0.5f - Mathf.Atan2(direction.Value.z, direction.Value.x) / (2f * Mathf.PI)) - positionOffsetPx) % uiTexture.width,
				y = uiTexture.height * (Mathf.Asin(direction.Value.y) / Mathf.PI + 0.5f)
			});
		}

		raycastResults.Clear();
		positionResults.Clear();
		foreach (var position in positions)
		{
			var tempData = new PointerEventData(eventSystem);
			tempData.Reset();

			tempData.position = position.Value;

			eventSystem.RaycastAll(tempData, m_RaycastResultCache);
			var result = FindFirstRaycast(m_RaycastResultCache);
			if (result.isValid)
			{
				raycastResults.Add(position.Key, result);
				positionResults.Add(position.Key, position.Value);
			}
			m_RaycastResultCache.Clear();
		}

		pointers.Clear();
		foreach (var kvp in raycastResults)
		{
			GetPointerData(kvp.Key, out var prevData, true);

			pointers.Add(kvp.Key, new PointerEventData(eventSystem)
			{
				delta = positionResults[kvp.Key] - prevData.position,
				position = positionResults[kvp.Key],
				scrollDelta = Input.mouseScrollDelta,
				button = PointerEventData.InputButton.Left,
				pointerCurrentRaycast = raycastResults[kvp.Key],
				pointerId = kvp.Key,
			});
		}

		//TODO(Simon): Add hover clickstate determination
		clickStates.Clear();
		clickStates.Add(leftControllerId, StateForControllerTrigger(leftController));
		clickStates.Add(rightControllerId, StateForControllerTrigger(rightController));
		clickStates.Add(kMouseLeftId, StateForMouseButton(0));

		foreach (var kvp in pointers)
		{
			if (!previousHovers.ContainsKey(kvp.Key))
			{
				previousHovers.Add(kvp.Key, null);
			}
			if (kvp.Value.pointerCurrentRaycast.gameObject != previousHovers[kvp.Key])
			{
				ExecuteEvents.ExecuteHierarchy(kvp.Value.pointerCurrentRaycast.gameObject, kvp.Value, ExecuteEvents.pointerEnterHandler);
				//NOTE(Simon): Check if any other pointers are hovering the object that's just been unhovered.
				var otherHovers = false;
				foreach (var currentHover in pointers)
				{
					if (currentHover.Value.pointerCurrentRaycast.gameObject == previousHovers[kvp.Key])
					{
						otherHovers = true;
					}
				}
				//NOTE(Simon): If no other hovers, send PointerExit Event.
				if (!otherHovers)
				{
					ExecuteEvents.ExecuteHierarchy(previousHovers[kvp.Key], kvp.Value, ExecuteEvents.pointerExitHandler);
				}
				previousHovers[kvp.Key] = kvp.Value.pointerCurrentRaycast.gameObject;
			}
			if (clickStates[kvp.Key] == PointerEventData.FramePressState.Pressed)
			{
				ExecuteEvents.ExecuteHierarchy(kvp.Value.pointerCurrentRaycast.gameObject, kvp.Value, ExecuteEvents.pointerDownHandler);
				ExecuteEvents.ExecuteHierarchy(kvp.Value.pointerCurrentRaycast.gameObject, kvp.Value, ExecuteEvents.initializePotentialDrag);
#if DEBUG_UI_INPUT_MODULE
				Debug.Log("Pointer down by " + kvp.Key + " on " + kvp.Value.pointerCurrentRaycast.gameObject + " in frame " + Time.frameCount);
#endif
			}
			if (clickStates[kvp.Key] == PointerEventData.FramePressState.Released || clickStates[kvp.Key] == PointerEventData.FramePressState.PressedAndReleased)
			{
				ExecuteEvents.ExecuteHierarchy(kvp.Value.pointerCurrentRaycast.gameObject, kvp.Value, ExecuteEvents.pointerClickHandler);
				ExecuteEvents.ExecuteHierarchy(kvp.Value.pointerCurrentRaycast.gameObject, kvp.Value, ExecuteEvents.pointerUpHandler);
#if DEBUG_UI_INPUT_MODULE
				Debug.Log("Click by " + kvp.Key + " on " + kvp.Value.pointerCurrentRaycast.gameObject + " in frame " + Time.frameCount);
#endif
			}
		}
	}

	public Vector2 ScreenPointToSpherePoint(Vector2 screenPoint)
	{
		var direction = camera.ScreenPointToRay((Vector2)Input.mousePosition).direction;
		float positionOffsetPx = offset / 360 * uiTexture.width;
		var position = new Vector2
		{
			x = (uiTexture.width * (0.5f - Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI)) - positionOffsetPx) % uiTexture.width,
			y = uiTexture.height * (Mathf.Asin(direction.y) / Mathf.PI + 0.5f)
		};

		return position;
	}
}